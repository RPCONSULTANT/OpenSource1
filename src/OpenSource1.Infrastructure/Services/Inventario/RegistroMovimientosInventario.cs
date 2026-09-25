using System.Globalization;
using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Inventario;

/// <summary>
/// Escritor único del libro de inventario. Toda la escritura va con Dapper sobre la conexión y la transacción de
/// <see cref="IDbSession"/> (<c>INSERT ... RETURNING "Id"</c>); el único UPDATE sobre el libro es el decremento de
/// <c>MovimientosProducto."CantidadRestante"</c>, por SQL directo de una sola columna (la rama que el trigger
/// append-only permite). Nunca se usa el change tracker de EF sobre entidades del libro.
/// </summary>
/// <remarks>
/// Las validaciones y la comprobación de existencia van ANTES del primer INSERT: un resultado fallido no deja filas
/// escritas en la transacción del llamador. Serializa por producto con <see cref="BloqueoInventarioProducto"/>.
/// </remarks>
public sealed class RegistroMovimientosInventario(
    IDbSession session,
    IConversionUnidadMedidaService conversion,
    IConsultaInventario consulta,
    IUsuarioActual usuario) : IRegistroMovimientosInventario
{
    private const int LongitudCreatedBy = 100;
    private const int LongitudClaveOrigen = 50;
    private const int LongitudNumeroDocumento = 20;

    /// <summary>Máximo de numeric(18,4), mismo criterio que <c>ProductoValidator</c>.</summary>
    private const decimal ImporteMaximo = 99_999_999_999_999.9999m;

    public async Task BloquearProductosAsync(IEnumerable<Guid> productoIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(productoIds);
        if (!session.HayTransaccionActiva)
        {
            throw new InvalidOperationException("Bloquear productos del inventario requiere una transacción activa.");
        }

        await session.EnsureOpenAsync(ct);

        // Orden total y único para todos los llamadores: así nunca hay dos transacciones esperándose en ciclo.
        foreach (var productoId in productoIds.Distinct().Order())
        {
            await BloqueoInventarioProducto.AdquirirAsync(session, productoId, ct);
        }
    }

    public async Task BloquearAlmacenExclusivoAsync(Guid almacenId, CancellationToken ct = default)
    {
        if (!session.HayTransaccionActiva)
        {
            throw new InvalidOperationException("Bloquear un almacén del inventario requiere una transacción activa.");
        }

        await session.EnsureOpenAsync(ct);
        await BloqueoInventarioAlmacen.AdquirirExclusivoAsync(session, almacenId, ct);
    }

    public async Task<Result<MovimientoRegistrado>> RegistrarAsync(MovimientoInventarioSolicitud solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        // 1. Transacción obligatoria: el advisory lock es de transacción y el llamador confirma todo el documento.
        if (!session.HayTransaccionActiva)
        {
            return Fallo(new Error(
                "inventario.sin_transaccion", "El registro de movimientos de inventario requiere una transacción activa."));
        }

        await session.EnsureOpenAsync(ct);
        var tx = session.CurrentTransaction;

        // 2. Serializa por producto hasta el commit/rollback: existencia, restantes FIFO y costo promedio se leen y
        //    escriben sin que otra transacción del mismo producto se intercale.
        await BloqueoInventarioProducto.AdquirirAsync(session, solicitud.ProductoId, ct);

        //    Después del producto (orden único de adquisición), el almacén en modo COMPARTIDO y ANTES de validarlo: un
        //    borrado del almacén (modo exclusivo) espera a este commit, o este registro espera al borrado y lo ve borrado.
        await BloqueoInventarioAlmacen.AdquirirCompartidoAsync(session, solicitud.AlmacenId, ct);

        // 3. Validaciones (referencias del cuerpo: ningún código termina en ".no_encontrado").
        if (solicitud.Cantidad <= 0)
        {
            return Fallo(new Error("inventario.cantidad_invalida", "La cantidad debe ser mayor que cero.", "Cantidad"));
        }

        var producto = await session.Connection.QuerySingleOrDefaultAsync<ProductoFila>(new CommandDefinition(
            """SELECT "Bloqueado", "CostoUnitario" FROM "Productos" WHERE "Id" = @Id AND "IsDeleted" = false""",
            new { Id = solicitud.ProductoId }, tx, cancellationToken: ct));
        if (producto is null)
        {
            return Fallo(new Error("inventario.producto_invalido", "El producto no existe.", "ProductoId"));
        }

        if (producto.Bloqueado == BloqueoProducto.Todo)
        {
            return Fallo(new Error("inventario.producto_bloqueado", "El producto está bloqueado para todo movimiento.", "ProductoId"));
        }

        var almacenBloqueado = await session.Connection.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(
            """SELECT "Bloqueado" FROM "Almacenes" WHERE "Id" = @Id AND "IsDeleted" = false""",
            new { Id = solicitud.AlmacenId }, tx, cancellationToken: ct));
        if (almacenBloqueado is null)
        {
            return Fallo(new Error("inventario.almacen_invalido", "El almacén no existe.", "AlmacenId"));
        }

        if (almacenBloqueado.Value)
        {
            return Fallo(new Error("inventario.almacen_bloqueado", "El almacén está bloqueado.", "AlmacenId"));
        }

        var esTransferencia = solicitud.TipoMovimiento == TipoMovimientoInventario.Transferencia;
        if (solicitud.EsEntrada
            && (solicitud.CostoUnitario < 0 || (!esTransferencia && solicitud.CostoUnitario is null)))
        {
            return Fallo(new Error(
                "inventario.costo_requerido",
                "Una entrada requiere un costo unitario mayor o igual que cero.", "CostoUnitario"));
        }

        // Solo se valida en entradas: en una salida CostoUnitario se ignora (el contrato no lo usa allí). La entrada de una
        // TRANSFERENCIA admite más de 4 decimales: recibe el costo exacto de su salida gemela (-ImporteCosto / CantidadBase,
        // p. ej. 5.3333 / 4 = 1.333325) para que su importe sea exactamente el opuesto; redondearlo a 4 decimales
        // descuadraría la reclasificación. CostoPorUnidad se sigue guardando redondeado a 4.
        if (solicitud.EsEntrada && solicitud.CostoUnitario is { } costoSolicitado
            && !(esTransferencia ? EsCostoTransferenciaValido(costoSolicitado) : EsImporteValido(costoSolicitado)))
        {
            return Fallo(new Error(
                "inventario.costo_invalido",
                "El costo unitario debe ser menor que 1e14 y tener como máximo 4 decimales.", "CostoUnitario"));
        }

        if (string.IsNullOrWhiteSpace(solicitud.ClaveOrigen) || solicitud.ClaveOrigen.Length > LongitudClaveOrigen)
        {
            return Fallo(new Error(
                "inventario.clave_origen_invalida",
                $"La clave de origen es obligatoria y admite como máximo {LongitudClaveOrigen} caracteres.", "ClaveOrigen"));
        }

        if (solicitud.NumeroDocumento is { Length: > LongitudNumeroDocumento })
        {
            return Fallo(new Error(
                "inventario.numero_documento_invalido",
                $"El número de documento admite como máximo {LongitudNumeroDocumento} caracteres.", "NumeroDocumento"));
        }

        // Solo se valida en salidas: en una entrada ImporteVenta se ignora y se guarda 0 (ver el INSERT de abajo).
        if (!solicitud.EsEntrada && !EsImporteValido(solicitud.ImporteVenta))
        {
            return Fallo(new Error(
                "inventario.importe_venta_invalido",
                "El importe de venta debe ser mayor o igual que cero, menor que 1e14 y tener como máximo 4 decimales.", "ImporteVenta"));
        }

        // 4. Cantidad en unidad base y factor congelado (el del servicio de conversión, nunca cantidadBase / cantidad).
        var cantidadBaseResultado = await conversion.ConvertirABaseAsync(
            solicitud.ProductoId, solicitud.UnidadMedidaId, solicitud.Cantidad, ct);
        if (cantidadBaseResultado.EsFallo)
        {
            return Result<MovimientoRegistrado>.Fallo(cantidadBaseResultado);
        }

        var factorResultado = await conversion.ObtenerFactorAsync(solicitud.ProductoId, solicitud.UnidadMedidaId, ct);
        if (factorResultado.EsFallo)
        {
            return Result<MovimientoRegistrado>.Fallo(factorResultado);
        }

        var cantidadBase = cantidadBaseResultado.Valor;
        if (cantidadBase <= 0)
        {
            // La cantidad convertida y redondeada a los decimales de la base quedó en cero.
            return Fallo(new Error(
                "inventario.cantidad_invalida",
                "La cantidad es demasiado pequeña para la precisión de la unidad base del producto.", "Cantidad"));
        }

        var factor = Math.Round(factorResultado.Valor, 6, MidpointRounding.AwayFromZero);
        var ahora = DateTimeOffset.UtcNow;
        var creadoPor = CreadoPor();
        var marcarAjuste = false;

        long movimientoProductoId;
        decimal importeCosto;
        decimal costoPorUnidad;
        decimal cantidadValorada;

        if (solicitud.EsEntrada)
        {
            // 5. Entrada. Una transferencia sin costo explícito se valora al promedio vigente (el de la salida gemela).
            var costo = solicitud.CostoUnitario;
            if (costo is null)
            {
                costo = await consulta.CostoPromedioAsync(solicitud.ProductoId, solicitud.FechaRegistro, ct);
                if (costo is null)
                {
                    costo = producto.CostoUnitario;
                    marcarAjuste = true;
                }
            }

            importeCosto = CostoPromedioCalculadora.Importe(cantidadBase, costo.Value);
            costoPorUnidad = CostoPromedioCalculadora.CostoPorUnidad(costo.Value);
            cantidadValorada = cantidadBase;
            movimientoProductoId = await InsertarMovimientoProductoAsync(solicitud, cantidadBase, cantidadBase, factor, ahora, creadoPor, ct);

            // 7. Ruling AS: TODA entrada de un producto que ya tiene alguna salida (de cualquier fecha) lo deja pendiente de
            //    ajuste. Una entrada anterior o del mismo día cambia el promedio de salidas ya registradas; una POSTERIOR
            //    puede cambiar el costo de un día sin costo calculable (Q <= 0), que la rutina de ajuste valora con el pool
            //    de entradas del primer día posterior que las tenga, o dejar resoluble un día que quedó pendiente.
            var tieneSalidas = await session.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                """SELECT EXISTS (SELECT 1 FROM "MovimientosProducto" WHERE "ProductoId" = @ProductoId AND "Cantidad" < 0)""",
                new { solicitud.ProductoId }, tx, cancellationToken: ct));
            if (tieneSalidas)
            {
                marcarAjuste = true;
            }
        }
        else
        {
            // 6. Salida: existencia a la fecha en el almacén y restante FIFO abierto; se exige el menor de ambos.
            var existencia = await consulta.ExistenciaAsync(solicitud.ProductoId, solicitud.AlmacenId, solicitud.FechaRegistro, ct);
            var restanteAbierto = await session.Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
                """
                SELECT COALESCE(SUM("CantidadRestante"), 0) FROM "MovimientosProducto"
                WHERE "ProductoId" = @ProductoId AND "AlmacenId" = @AlmacenId
                  AND "CantidadRestante" > 0 AND "FechaRegistro" <= @FechaRegistro
                """,
                new { solicitud.ProductoId, solicitud.AlmacenId, solicitud.FechaRegistro }, tx, cancellationToken: ct));

            var disponible = Math.Min(existencia, restanteAbierto);
            if (disponible < cantidadBase)
            {
                return Fallo(new Error(
                    "inventario.existencia_insuficiente",
                    $"Existencia insuficiente en el almacén a la fecha {solicitud.FechaRegistro:yyyy-MM-dd}: disponible " +
                    $"{Formato(disponible)}, solicitado {Formato(cantidadBase)} (unidad base).",
                    "Cantidad"));
            }

            var costo = await consulta.CostoPromedioAsync(solicitud.ProductoId, solicitud.FechaRegistro, ct);
            if (costo is null)
            {
                costo = producto.CostoUnitario;
            }

            importeCosto = -CostoPromedioCalculadora.Importe(cantidadBase, costo.Value);
            costoPorUnidad = CostoPromedioCalculadora.CostoPorUnidad(costo.Value);
            cantidadValorada = -cantidadBase;
            movimientoProductoId = await InsertarMovimientoProductoAsync(solicitud, -cantidadBase, null, factor, ahora, creadoPor, ct);
            await AplicarFifoAsync(solicitud, movimientoProductoId, cantidadBase, ct);

            // 7. Toda salida deja el costo pendiente de ajuste (su costo puede cambiar con entradas retroactivas).
            marcarAjuste = true;
        }

        var movimientoValorId = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO "MovimientosValor" (
                "MovimientoProductoId", "ProductoId", "AlmacenId", "TipoValor", "TipoMovimiento", "FechaRegistro",
                "CantidadValorada", "CantidadFacturada", "ImporteCosto", "CostoPorUnidad", "ImporteVenta",
                "ImporteCostoPosteadoContabilidad", "Ajuste", "TipoDocumento", "NumeroDocumento", "NumeroLineaDocumento",
                "TipoOrigen", "ClaveOrigen", "CreatedAtUtc", "CreatedBy", "UsuarioId")
            VALUES (
                @MovimientoProductoId, @ProductoId, @AlmacenId, @TipoValor, @TipoMovimiento, @FechaRegistro,
                @CantidadValorada, 0, @ImporteCosto, @CostoPorUnidad, @ImporteVenta,
                0, false, @TipoDocumento, @NumeroDocumento, @NumeroLineaDocumento,
                @TipoOrigen, @ClaveOrigen, @CreatedAtUtc, @CreatedBy, @UsuarioId)
            RETURNING "Id"
            """,
            new
            {
                MovimientoProductoId = movimientoProductoId,
                solicitud.ProductoId,
                solicitud.AlmacenId,
                TipoValor = TipoValor.CostoDirecto,
                solicitud.TipoMovimiento,
                solicitud.FechaRegistro,
                CantidadValorada = cantidadValorada,
                ImporteCosto = importeCosto,
                CostoPorUnidad = costoPorUnidad,
                ImporteVenta = solicitud.EsEntrada ? 0m : solicitud.ImporteVenta,
                solicitud.TipoDocumento,
                solicitud.NumeroDocumento,
                solicitud.NumeroLineaDocumento,
                solicitud.TipoOrigen,
                solicitud.ClaveOrigen,
                CreatedAtUtc = ahora,
                CreatedBy = creadoPor,
                UsuarioId = usuario.Id,
            },
            tx, cancellationToken: ct));

        if (marcarAjuste)
        {
            // Una sola columna y solo si cambia: no se pisa el resto del maestro (xmin cambia de todos modos; es inevitable).
            await session.Connection.ExecuteAsync(new CommandDefinition(
                """UPDATE "Productos" SET "CostoAjustado" = false WHERE "Id" = @ProductoId AND "CostoAjustado" = true""",
                new { solicitud.ProductoId }, tx, cancellationToken: ct));
        }

        return Result<MovimientoRegistrado>.Exito(
            new MovimientoRegistrado(movimientoProductoId, movimientoValorId, cantidadBase, importeCosto));
    }

    private async Task<long> InsertarMovimientoProductoAsync(
        MovimientoInventarioSolicitud solicitud, decimal cantidad, decimal? cantidadRestante, decimal factor,
        DateTimeOffset ahora, string creadoPor, CancellationToken ct) =>
        await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO "MovimientosProducto" (
                "ProductoId", "AlmacenId", "TipoMovimiento", "TipoDocumento", "NumeroDocumento", "NumeroLineaDocumento",
                "FechaRegistro", "FechaDocumento", "Cantidad", "CantidadRestante", "CantidadFacturada", "UnidadMedidaId",
                "CantidadPorUnidadMedida", "SocioNegocioId", "TipoOrigen", "ClaveOrigen", "CreatedAtUtc", "CreatedBy", "UsuarioId")
            VALUES (
                @ProductoId, @AlmacenId, @TipoMovimiento, @TipoDocumento, @NumeroDocumento, @NumeroLineaDocumento,
                @FechaRegistro, @FechaDocumento, @Cantidad, @CantidadRestante, 0, @UnidadMedidaId,
                @Factor, @SocioNegocioId, @TipoOrigen, @ClaveOrigen, @CreatedAtUtc, @CreatedBy, @UsuarioId)
            RETURNING "Id"
            """,
            new
            {
                solicitud.ProductoId,
                solicitud.AlmacenId,
                solicitud.TipoMovimiento,
                solicitud.TipoDocumento,
                solicitud.NumeroDocumento,
                solicitud.NumeroLineaDocumento,
                solicitud.FechaRegistro,
                solicitud.FechaDocumento,
                Cantidad = cantidad,
                CantidadRestante = cantidadRestante,
                solicitud.UnidadMedidaId,
                Factor = factor,
                solicitud.SocioNegocioId,
                solicitud.TipoOrigen,
                solicitud.ClaveOrigen,
                CreatedAtUtc = ahora,
                CreatedBy = creadoPor,
                UsuarioId = usuario.Id,
            },
            session.CurrentTransaction, cancellationToken: ct));

    /// <summary>
    /// Consume las entradas abiertas del almacén con fecha &lt;= la de la salida, en orden (FechaRegistro, Id), bloqueándolas
    /// FOR UPDATE; cada consumo decrementa SOLO "CantidadRestante" e inserta una aplicación.
    /// </summary>
    private async Task AplicarFifoAsync(
        MovimientoInventarioSolicitud solicitud, long salidaId, decimal cantidadBase, CancellationToken ct)
    {
        var tx = session.CurrentTransaction;
        var abiertas = await session.Connection.QueryAsync<(long Id, decimal Restante)>(new CommandDefinition(
            """
            SELECT "Id", "CantidadRestante" FROM "MovimientosProducto"
            WHERE "ProductoId" = @ProductoId AND "AlmacenId" = @AlmacenId
              AND "CantidadRestante" > 0 AND "FechaRegistro" <= @FechaRegistro
            ORDER BY "FechaRegistro", "Id"
            FOR UPDATE
            """,
            new { solicitud.ProductoId, solicitud.AlmacenId, solicitud.FechaRegistro }, tx, cancellationToken: ct));

        var pendiente = cantidadBase;
        foreach (var (entradaId, restante) in abiertas)
        {
            if (pendiente <= 0)
            {
                break;
            }

            var aplicada = Math.Min(restante, pendiente);

            await session.Connection.ExecuteAsync(new CommandDefinition(
                """UPDATE "MovimientosProducto" SET "CantidadRestante" = "CantidadRestante" - @Aplicada WHERE "Id" = @Id""",
                new { Aplicada = aplicada, Id = entradaId }, tx, cancellationToken: ct));

            await session.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO "AplicacionesMovimientoProducto" ("MovimientoEntradaId", "MovimientoSalidaId", "Cantidad", "FechaRegistro")
                VALUES (@EntradaId, @SalidaId, @Cantidad, @FechaRegistro)
                """,
                new { EntradaId = entradaId, SalidaId = salidaId, Cantidad = aplicada, solicitud.FechaRegistro }, tx, cancellationToken: ct));

            pendiente -= aplicada;
        }

        if (pendiente > 0)
        {
            // Imposible bajo el advisory lock (el restante abierto se comprobó antes de escribir). Si ocurre, algún escritor
            // del libro no tomó el bloqueo del producto: se aborta en vez de dejar una salida parcialmente aplicada.
            throw new InvalidOperationException(
                $"Aplicación FIFO incompleta para el producto {solicitud.ProductoId}: faltan {Formato(pendiente)} unidades " +
                "por aplicar tras validar la existencia. Otro escritor modificó el libro sin el bloqueo por producto.");
        }
    }

    private string CreadoPor()
    {
        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        return nombre.Length <= LongitudCreatedBy ? nombre : nombre[..LongitudCreatedBy];
    }

    /// <summary>numeric(18,4): 0 &lt;= valor &lt;= 99 999 999 999 999.9999 y sin más de 4 decimales (Postgres redondearía en silencio).</summary>
    private static bool EsImporteValido(decimal valor) =>
        valor >= 0 && valor <= ImporteMaximo && decimal.Round(valor, 4) == valor;

    /// <summary>Rango de numeric(18,4) sin exigir 4 decimales (solo el costo de la entrada gemela de una transferencia).</summary>
    private static bool EsCostoTransferenciaValido(decimal valor) => valor >= 0 && valor <= ImporteMaximo;

    private static string Formato(decimal valor) => valor.ToString("0.######", CultureInfo.InvariantCulture);

    private static Result<MovimientoRegistrado> Fallo(Error error) => Result<MovimientoRegistrado>.Fallo(error);

    private sealed record ProductoFila(BloqueoProducto Bloqueado, decimal CostoUnitario);
}
