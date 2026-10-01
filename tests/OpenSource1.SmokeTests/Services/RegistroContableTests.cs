using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// <see cref="IRegistroContable"/> contra Postgres real (Task 5.5): asiento balanceado con todos sus campos (Debito/Credito,
/// número de cuenta congelado, grupos, rango del registro); Review Focus 1 (descuadre: <c>Result</c> fallido y NADA escrito, ni
/// siquiera el número de la serie); validaciones de líneas y cuentas; numeración <c>CONTAB</c> sin huecos (también en paralelo);
/// y el trigger append-only del libro contable. Cada test crea sus propias cuentas (datos aislados por Guid). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistroContableTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly Fecha = new(2026, 9, 20);
    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    // ── Asiento balanceado ──

    [Fact]
    public async Task Balanceado_EscribeRegistroYMovimientos_ConDesgloseNumeroCongeladoYGrupos()
    {
        var caja = await CuentaAsync();
        var ventas = await CuentaAsync();
        var productoId = await _prueba.SembrarProductoAsync();
        var clave = $"T-{Guid.NewGuid():N}"[..30];
        var asiento = new AsientoContable(
            Fecha, Fecha.AddDays(-1), TipoDocumentoContable.FacturaVenta, "FV-0001", "Venta de contado",
            TipoOrigenMovimiento.FacturaVenta, clave,
            [
                new LineaAsiento(caja, 118.5m, null),
                new LineaAsiento(ventas, -100m, "Ventas de bienes", ProductoId: productoId,
                    GrupoNegocioId: GrupoContableIds.NegocioNacional, GrupoProductoId: GrupoContableIds.ProductoBienes),
                new LineaAsiento(ventas, -18.5m, "ITBIS", GrupoIvaNegocioId: GrupoContableIds.IvaNegocioItbis18,
                    GrupoIvaProductoId: GrupoContableIds.IvaProductoItbis18),
            ]);

        var registrado = Ok(await RegistrarAsync(asiento));

        Assert.Equal(registrado.DesdeMovimiento + 2, registrado.HastaMovimiento);
        Assert.Matches("^[0-9]{8}$", registrado.NumeroRegistro);

        await using var conexion = _prueba.NuevaConexion();
        var registro = await conexion.QuerySingleAsync<(string Numero, long Desde, long Hasta, short TipoOrigen, string Clave, string CreadoPor)>(
            """
            SELECT "NumeroRegistro", "DesdeMovimiento", "HastaMovimiento", "TipoOrigen", "ClaveOrigen", "CreadoPor"
            FROM "RegistrosContables" WHERE "Id" = @Id
            """, new { Id = registrado.RegistroContableId });
        Assert.Equal((registrado.NumeroRegistro, registrado.DesdeMovimiento, registrado.HastaMovimiento, (short)TipoOrigenMovimiento.FacturaVenta, clave, "system"), registro);

        var movimientos = (await conexion.QueryAsync<MovimientoFila>(
            """
            SELECT "Id", "CuentaContableId", "NumeroCuenta", "FechaRegistro", "FechaDocumento", "TipoDocumento", "NumeroDocumento",
                   "Descripcion", "Importe", "Debito", "Credito", "ProductoId", "GrupoNegocioId", "GrupoProductoId",
                   "GrupoIvaNegocioId", "GrupoIvaProductoId", "TipoOrigen", "ClaveOrigen"
            FROM "MovimientosContables" WHERE "RegistroContableId" = @Id ORDER BY "Id"
            """, new { Id = registrado.RegistroContableId })).ToList();

        Assert.Equal(3, movimientos.Count);
        Assert.Equal(registrado.DesdeMovimiento, movimientos[0].Id);
        Assert.Equal(registrado.HastaMovimiento, movimientos[2].Id);
        Assert.Equal(0m, movimientos.Sum(x => x.Importe));
        Assert.All(movimientos, m =>
        {
            Assert.Equal(Fecha, m.FechaRegistro);
            Assert.Equal(Fecha.AddDays(-1), m.FechaDocumento);
            Assert.Equal((short)TipoDocumentoContable.FacturaVenta, m.TipoDocumento);
            Assert.Equal("FV-0001", m.NumeroDocumento);
            Assert.Equal((short)TipoOrigenMovimiento.FacturaVenta, m.TipoOrigen);
            Assert.Equal(clave, m.ClaveOrigen);
        });

        // Débito: max(Importe, 0); crédito: max(-Importe, 0). Número de cuenta congelado; descripción de la línea o del asiento.
        Assert.Equal((118.5m, 0m), (movimientos[0].Debito, movimientos[0].Credito));
        Assert.Equal((0m, 100m), (movimientos[1].Debito, movimientos[1].Credito));
        Assert.Equal((0m, 18.5m), (movimientos[2].Debito, movimientos[2].Credito));
        Assert.Equal(await NumeroCuentaAsync(caja), movimientos[0].NumeroCuenta);
        Assert.Equal(await NumeroCuentaAsync(ventas), movimientos[1].NumeroCuenta);
        Assert.Equal("Venta de contado", movimientos[0].Descripcion);
        Assert.Equal("Ventas de bienes", movimientos[1].Descripcion);
        Assert.Equal(productoId, movimientos[1].ProductoId);
        Assert.Equal((GrupoContableIds.NegocioNacional, GrupoContableIds.ProductoBienes), (movimientos[1].GrupoNegocioId, movimientos[1].GrupoProductoId));
        Assert.Equal((GrupoContableIds.IvaNegocioItbis18, GrupoContableIds.IvaProductoItbis18), (movimientos[2].GrupoIvaNegocioId, movimientos[2].GrupoIvaProductoId));
        Assert.Null(movimientos[0].GrupoNegocioId);
    }

    [Fact]
    public async Task NumeroCuenta_QuedaCongelado_AunqueLaCuentaSeRenumereDespues()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        var numeroOriginal = await NumeroCuentaAsync(a);
        var registrado = Ok(await RegistrarAsync(Asiento((a, 10m), (b, -10m))));

        await using var contexto = _prueba.NuevoContexto();
        var cuenta = await contexto.CuentasContables.SingleAsync(x => x.Id == a);
        cuenta.Numero = NuevoNumero();
        await contexto.SaveChangesAsync();

        await using var conexion = _prueba.NuevaConexion();
        var congelado = await conexion.QuerySingleAsync<string>(
            """SELECT "NumeroCuenta" FROM "MovimientosContables" WHERE "Id" = @Id""", new { Id = registrado.DesdeMovimiento });
        Assert.Equal(numeroOriginal, congelado);
    }

    // ── Review Focus 1: descuadre ──

    [Fact]
    public async Task Descuadrado_DevuelveFalloConElDescuadre()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();

        var resultado = await RegistrarAsync(Asiento((a, 100.0001m), (b, -100m)));

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("contabilidad.asiento_descuadrado", error.Codigo);
        Assert.Contains("0.0001", error.Mensaje);
        Assert.Contains("100.0001", error.Mensaje);
    }

    /// <summary>
    /// Red del Review Focus 1: pase lo que pase dentro del servicio (<c>Result</c> fallido por la validación en memoria o
    /// excepción del <c>SELECT SUM</c> posterior si esa validación faltara), el llamador no confirma y no queda NADA: ni
    /// registro, ni movimientos, ni número consumido de la serie.
    /// </summary>
    [Fact]
    public async Task Descuadrado_NoDejaNadaEscrito_NiConsumeLaSerie()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        var antes = await EstadoAsync();

        var (resultado, excepcion) = await IntentarRegistrarAsync(Asiento((a, 50m), (b, -49.9999m)));

        Assert.True(excepcion is not null || resultado is { EsFallo: true });
        Assert.Equal(antes, await EstadoAsync());
        Assert.Equal(0L, await MovimientosDeCuentaAsync(a));
        Assert.Equal(0L, await RegistrosSinCuadrarAsync());
    }

    // ── Validaciones ──

    [Fact]
    public async Task SinLineas_ImporteCero_YMasDeCuatroDecimales_SeRechazanSinEscribir()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        var antes = await EstadoAsync();

        var vacio = await RegistrarAsync(Asiento());
        Assert.Equal("contabilidad.asiento_vacio", Assert.Single(vacio.Errores).Codigo);

        var cero = await RegistrarAsync(Asiento((a, 0m), (b, 0m)));
        Assert.Equal(["contabilidad.importe_invalido", "contabilidad.importe_invalido"], cero.Errores.Select(e => e.Codigo));
        Assert.Equal("Lineas[1].Importe", cero.Errores[1].Campo);

        var decimales = await RegistrarAsync(Asiento((a, 1.00001m), (b, -1.00001m)));
        Assert.All(decimales.Errores, e => Assert.Equal("contabilidad.importe_invalido", e.Codigo));

        var cabecera = await RegistrarAsync(new AsientoContable(
            Fecha, Fecha, TipoDocumentoContable.Ninguno, new string('9', 21), " ", TipoOrigenMovimiento.Diario, "",
            [new LineaAsiento(a, 1m, null), new LineaAsiento(b, -1m, new string('x', 201))]));
        Assert.Equal(
            ["contabilidad.descripcion_invalida", "contabilidad.clave_origen_invalida", "contabilidad.numero_documento_invalido", "contabilidad.descripcion_invalida"],
            cabecera.Errores.Select(e => e.Codigo));

        Assert.Equal(antes, await EstadoAsync());
    }

    [Fact]
    public async Task CuentaInexistenteBorradaNoPosteoOBloqueada_SeRechazaConElCampoDeLaLinea_SinEscribir()
    {
        var buena = await CuentaAsync();
        var borrada = await CuentaAsync(borrada: true);
        var encabezado = await CuentaAsync(TipoCuentaContable.Encabezado);
        var total = await CuentaAsync(TipoCuentaContable.Total);
        var bloqueada = await CuentaAsync(bloqueada: true);
        var antes = await EstadoAsync();

        var resultado = await RegistrarAsync(Asiento(
            (buena, 50m), (Guid.NewGuid(), -10m), (borrada, -10m), (encabezado, -10m), (total, -10m), (bloqueada, -10m)));

        Assert.Equal(
            [
                ("contabilidad.cuenta_invalida", "Lineas[1].CuentaContableId"),
                ("contabilidad.cuenta_invalida", "Lineas[2].CuentaContableId"),
                ("contabilidad.cuenta_no_posteo", "Lineas[3].CuentaContableId"),
                ("contabilidad.cuenta_no_posteo", "Lineas[4].CuentaContableId"),
                ("contabilidad.cuenta_bloqueada", "Lineas[5].CuentaContableId"),
            ],
            resultado.Errores.Select(e => (e.Codigo, e.Campo)));
        Assert.Equal(antes, await EstadoAsync());
    }

    [Fact]
    public async Task SinTransaccion_SeRechaza()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        await using var scope = _prueba.Provider.CreateAsyncScope();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroContable>();

        var resultado = await registro.RegistrarAsync(Asiento((a, 1m), (b, -1m)));

        Assert.Equal("contabilidad.sin_transaccion", Assert.Single(resultado.Errores).Codigo);
    }

    // ── Numeración CONTAB ──

    [Fact]
    public async Task Numeracion_ConsecutivaYSinHuecos_UnFalloNoConsumeNumero()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();

        var primero = Ok(await RegistrarAsync(Asiento((a, 1m), (b, -1m))));
        Assert.True((await RegistrarAsync(Asiento((a, 1m), (b, -2m)))).EsFallo);
        Assert.True((await RegistrarAsync(Asiento((a, 1m), (Guid.NewGuid(), -1m)))).EsFallo);
        var segundo = Ok(await RegistrarAsync(Asiento((a, 2m), (b, -2m))));

        Assert.Equal(long.Parse(primero.NumeroRegistro) + 1, long.Parse(segundo.NumeroRegistro));
        // La línea de serie guarda el último número sin ceros a la izquierda (GeneradorNumeroDocumento).
        Assert.Equal(long.Parse(segundo.NumeroRegistro), long.Parse((await EstadoAsync()).UltimoNumero));

        // Cada registro tiene su rango contiguo (Desde..Hasta = sus líneas) y el siguiente empieza DESPUÉS del anterior. No se
        // exige que empiece justo a continuación: un rollback tras reservar los ids (nextval) deja huecos inocuos entre registros.
        Assert.Equal(primero.DesdeMovimiento + 1, primero.HastaMovimiento);
        Assert.True(segundo.DesdeMovimiento > primero.HastaMovimiento);
    }

    [Fact]
    public async Task Numeracion_EnParalelo_SinDuplicadosNiHuecosDeNumero_YRangosDeMovimientosDisjuntos()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        const int n = 8;

        var resultados = await Task.WhenAll(Enumerable.Range(1, n).Select(i =>
            Task.Run(() => RegistrarAsync(Asiento((a, i), (b, -i), (a, 1m), (b, -1m))))));

        var registrados = resultados.Select(Ok).OrderBy(x => x.NumeroRegistro, StringComparer.Ordinal).ToList();
        var numeros = registrados.Select(x => long.Parse(x.NumeroRegistro)).ToList();
        Assert.Equal(Enumerable.Range(0, n).Select(i => numeros[0] + i), numeros);
        Assert.All(registrados, r => Assert.Equal(r.DesdeMovimiento + 3, r.HastaMovimiento));
        for (var i = 1; i < n; i++)
        {
            // Serializados por el bloqueo del libro: el orden de número coincide con el de ids y los rangos son disjuntos (cada uno contiguo
            // por dentro, comprobado arriba; entre registros podría haber huecos inocuos de un rollback tras nextval).
            Assert.True(registrados[i].DesdeMovimiento > registrados[i - 1].HastaMovimiento);
        }

        Assert.Equal(0L, await RegistrosSinCuadrarAsync());
    }

    /// <summary>Patrón del batch de costo (Task 5.6): varios asientos dentro de UNA transacción del llamador.</summary>
    [Fact]
    public async Task DosRegistros_EnLaMismaTransaccion_CuadranConRangosPropiosYNumerosConsecutivos()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();

        await using var scope = _prueba.Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroContable>();
        AsientoRegistrado primero, segundo;
        await using (await sesion.BeginTransactionAsync())
        {
            primero = Ok(await registro.RegistrarAsync(Asiento((a, 5m), (b, -2m), (b, -3m))));
            segundo = Ok(await registro.RegistrarAsync(Asiento((b, 7m), (a, -7m))));
            await sesion.CommitAsync();
        }

        Assert.Equal(long.Parse(primero.NumeroRegistro) + 1, long.Parse(segundo.NumeroRegistro));
        Assert.Equal((primero.DesdeMovimiento + 2, segundo.DesdeMovimiento + 1), (primero.HastaMovimiento, segundo.HastaMovimiento));
        Assert.True(segundo.DesdeMovimiento > primero.HastaMovimiento);

        await using var conexion = _prueba.NuevaConexion();
        var porRegistro = (await conexion.QueryAsync<(long Registro, long Min, long Max, long Filas, decimal Suma)>(
            """
            SELECT "RegistroContableId", MIN("Id"), MAX("Id"), COUNT(*), SUM("Importe") FROM "MovimientosContables"
            WHERE "RegistroContableId" IN (@P, @S) GROUP BY "RegistroContableId" ORDER BY "RegistroContableId"
            """, new { P = primero.RegistroContableId, S = segundo.RegistroContableId })).ToList();
        Assert.Equal(
            [
                (primero.RegistroContableId, primero.DesdeMovimiento, primero.HastaMovimiento, 3L, 0m),
                (segundo.RegistroContableId, segundo.DesdeMovimiento, segundo.HastaMovimiento, 2L, 0m),
            ],
            porRegistro);
    }

    /// <summary>
    /// Ruling NSC2: el libro contable se serializa con su propio advisory lock, no con la línea de la serie. T1 registra sin
    /// confirmar; se reasigna AsientoContable a una segunda serie (confirmado); T2 (que ya numeraría con otra línea) espera a que
    /// T1 termine, y cada registro conserva su rango de ids contiguo.
    /// </summary>
    [Fact]
    public async Task ReasignarLaSerieDeAsientos_ConUnRegistroEnVuelo_ElSegundoEsperaAlBloqueoDelLibro()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        Guid otraSerie;
        await using (var contexto = _prueba.NuevoContexto())
        {
            var serie = new Serie
            {
                Codigo = $"AS{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                Descripcion = "Segunda serie de asientos",
                TipoDocumento = TipoDocumentoSerie.AsientoContable,
            };
            contexto.Series.Add(serie);
            contexto.LineasSerie.Add(new LineaSerie
            {
                SerieId = serie.Id, NumeroInicial = "ALT-000001", NumeroFinal = "ALT-999999", UltimoNumeroUsado = "",
                FechaInicial = new DateOnly(2020, 1, 1), Incremento = 1,
            });
            await contexto.SaveChangesAsync();
            otraSerie = serie.Id;
        }

        await using var scope = _prueba.Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroContable>();
        try
        {
            AsientoRegistrado primero;
            Task<Result<AsientoRegistrado>> segundoTask;
            await using (await sesion.BeginTransactionAsync())
            {
                primero = Ok(await registro.RegistrarAsync(Asiento((a, 5m), (b, -2m), (b, -3m))));
                await ConfigurarSerieDeAsientosAsync(otraSerie);

                segundoTask = Task.Run(() => RegistrarAsync(Asiento((b, 7m), (a, -7m))));
                var terminoAntes = await Task.WhenAny(segundoTask, Task.Delay(TimeSpan.FromSeconds(2))) == segundoTask;
                Assert.False(terminoAntes, "El segundo registro no esperó al bloqueo del libro contable.");

                await sesion.CommitAsync();
            }

            var segundo = Ok(await segundoTask.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.StartsWith("ALT-", segundo.NumeroRegistro);
            Assert.Equal((primero.DesdeMovimiento + 2, segundo.DesdeMovimiento + 1), (primero.HastaMovimiento, segundo.HastaMovimiento));
            Assert.True(segundo.DesdeMovimiento > primero.HastaMovimiento);
            Assert.Equal(0L, await RegistrosSinCuadrarAsync());
        }
        finally
        {
            await ConfigurarSerieDeAsientosAsync(SerieContabilidadIds.SerieId);
            await using var conexion = _prueba.NuevaConexion();
            await conexion.ExecuteAsync("""UPDATE "Series" SET "IsDeleted" = true WHERE "Id" = @Id""", new { Id = otraSerie });
        }
    }

    private async Task ConfigurarSerieDeAsientosAsync(Guid serieId)
    {
        await using var conexion = _prueba.NuevaConexion();
        await conexion.ExecuteAsync(
            """UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = @T AND "IsDeleted" = false""",
            new { S = serieId, T = (short)TipoDocumentoSerie.AsientoContable });
    }

    /// <summary>
    /// Última red (constraint trigger diferido, migración <c>VerificarCuadreLibroContable</c>): aunque alguien escriba el libro
    /// por fuera del servicio o capture su excepción y confirme, un registro descuadrado hace fallar el COMMIT (23514) y no
    /// queda nada.
    /// </summary>
    [Fact]
    public async Task CommitDeUnRegistroDescuadrado_PorSqlDirecto_FallaYNoQuedaNada()
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        var numero = $"X{Guid.NewGuid():N}"[..20];

        await using var conexion = _prueba.NuevaConexion();
        await conexion.OpenAsync();
        await using (var tx = await conexion.BeginTransactionAsync())
        {
            var registroId = await conexion.ExecuteScalarAsync<long>(
                """
                INSERT INTO "RegistrosContables" ("NumeroRegistro","DesdeMovimiento","HastaMovimiento","FechaCreacion","CreadoPor","TipoOrigen","ClaveOrigen")
                VALUES (@Numero, 0, 0, now(), 'test', 1, 'X') RETURNING "Id"
                """, new { Numero = numero }, tx);
            await conexion.ExecuteAsync(
                """
                INSERT INTO "MovimientosContables" ("CuentaContableId","NumeroCuenta","FechaRegistro","FechaDocumento","TipoDocumento",
                    "Descripcion","Importe","Debito","Credito","RegistroContableId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
                VALUES (@A,'1','2026-01-01','2026-01-01',0,'x',10,10,0,@R,1,'X',now(),'test'),
                       (@B,'2','2026-01-01','2026-01-01',0,'x',-9.9999,0,9.9999,@R,1,'X',now(),'test')
                """, new { A = a, B = b, R = registroId }, tx);

            var error = await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync());
            Assert.Equal("23514", error.SqlState);
        }

        Assert.Equal(0L, await conexion.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "RegistrosContables" WHERE "NumeroRegistro" = @Numero""", new { Numero = numero }));
        Assert.Equal(0L, await MovimientosDeCuentaAsync(a));
    }

    // ── Append-only en la base ──

    [Theory]
    [InlineData("""UPDATE "MovimientosContables" SET "Descripcion" = 'x' WHERE "Id" = @Id""")]
    [InlineData("""DELETE FROM "MovimientosContables" WHERE "Id" = @Id""")]
    [InlineData("""UPDATE "RegistrosContables" SET "ClaveOrigen" = 'x' WHERE "DesdeMovimiento" = @Id""")]
    [InlineData("""DELETE FROM "RegistrosContables" WHERE "DesdeMovimiento" = @Id""")]
    [InlineData("""TRUNCATE "MovimientosContables" CASCADE""")]
    [InlineData("""TRUNCATE "RegistrosContables" CASCADE""")]
    public async Task ElLibroContable_RechazaUpdateDeleteYTruncate(string sql)
    {
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        var registrado = Ok(await RegistrarAsync(Asiento((a, 3m), (b, -3m))));

        await using var conexion = _prueba.NuevaConexion();
        var error = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(sql, new { Id = registrado.DesdeMovimiento }));

        Assert.Equal("P0001", error.SqlState);
        Assert.Contains("solo inserción", error.MessageText);
    }

    // ── Helpers ──

    private static AsientoContable Asiento(params (Guid Cuenta, decimal Importe)[] lineas) => new(
        Fecha, Fecha, TipoDocumentoContable.Ninguno, null, "Asiento de prueba", TipoOrigenMovimiento.Diario,
        $"T-{Guid.NewGuid():N}"[..30], [.. lineas.Select(x => new LineaAsiento(x.Cuenta, x.Importe, null))]);

    /// <summary>Registra en su propio scope y transacción; confirma si tuvo éxito, deshace si falló.</summary>
    private async Task<Result<AsientoRegistrado>> RegistrarAsync(AsientoContable asiento)
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroContable>();

        await using var tx = await sesion.BeginTransactionAsync();
        var resultado = await registro.RegistrarAsync(asiento);
        if (resultado.EsExito)
        {
            await sesion.CommitAsync();
        }
        else
        {
            await sesion.RollbackAsync();
        }

        return resultado;
    }

    /// <summary>Como <see cref="RegistrarAsync"/>, pero una excepción del servicio se captura (la transacción se deshace al salir).</summary>
    private async Task<(Result<AsientoRegistrado>? Resultado, Exception? Excepcion)> IntentarRegistrarAsync(AsientoContable asiento)
    {
        try
        {
            return (await RegistrarAsync(asiento), null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private static AsientoRegistrado Ok(Result<AsientoRegistrado> resultado)
    {
        Assert.True(resultado.EsExito, resultado.EsFallo
            ? string.Join("; ", resultado.Errores.Select(e => $"{e.Codigo}: {e.Mensaje}"))
            : string.Empty);
        return resultado.Valor;
    }

    private sealed record Estado(long Registros, long Movimientos, string UltimoNumero);

    private async Task<Estado> EstadoAsync()
    {
        await using var conexion = _prueba.NuevaConexion();
        var (registros, movimientos, ultimo) = await conexion.QuerySingleAsync<(long, long, string)>(
            """
            SELECT (SELECT COUNT(*) FROM "RegistrosContables"), (SELECT COUNT(*) FROM "MovimientosContables"),
                   (SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Linea)
            """, new { Linea = SerieContabilidadIds.LineaSerieId });
        return new Estado(registros, movimientos, ultimo);
    }

    private async Task<long> MovimientosDeCuentaAsync(Guid cuentaId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "MovimientosContables" WHERE "CuentaContableId" = @Id""", new { Id = cuentaId });
    }

    /// <summary>Invariante dura del spec 5.5 sobre TODO el libro: ningún registro con SUM(Importe) &lt;&gt; 0.</summary>
    private async Task<long> RegistrosSinCuadrarAsync()
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*) FROM (
                SELECT r."Id" FROM "RegistrosContables" r
                LEFT JOIN "MovimientosContables" m ON m."RegistroContableId" = r."Id"
                GROUP BY r."Id" HAVING COALESCE(SUM(m."Importe"), 0) <> 0 OR COUNT(m."Id") = 0) x
            """);
    }

    private async Task<Guid> CuentaAsync(
        TipoCuentaContable tipo = TipoCuentaContable.Posteo, bool bloqueada = false, bool borrada = false)
    {
        await using var contexto = _prueba.NuevoContexto();
        var cuenta = new CuentaContable
        {
            Numero = NuevoNumero(),
            Nombre = "Cuenta de prueba del libro contable",
            TipoCuenta = tipo,
            TipoResultado = TipoResultadoCuenta.Balance,
            Bloqueada = bloqueada,
            IsDeleted = borrada,
            CreatedBy = "test",
        };
        contexto.CuentasContables.Add(cuenta);
        await contexto.SaveChangesAsync();
        return cuenta.Id;
    }

    private async Task<string> NumeroCuentaAsync(Guid id)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<string>("""SELECT "Numero" FROM "CuentasContables" WHERE "Id" = @Id""", new { Id = id })
            ?? throw new InvalidOperationException("Cuenta inexistente.");
    }

    private static long _contador = DateTime.UtcNow.Ticks % 1_000_000;

    private static string NuevoNumero() => $"8{Interlocked.Increment(ref _contador)}";

    private sealed class MovimientoFila
    {
        public long Id { get; init; }
        public Guid CuentaContableId { get; init; }
        public string NumeroCuenta { get; init; } = string.Empty;
        public DateOnly FechaRegistro { get; init; }
        public DateOnly FechaDocumento { get; init; }
        public short TipoDocumento { get; init; }
        public string? NumeroDocumento { get; init; }
        public string Descripcion { get; init; } = string.Empty;
        public decimal Importe { get; init; }
        public decimal Debito { get; init; }
        public decimal Credito { get; init; }
        public Guid? ProductoId { get; init; }
        public Guid? GrupoNegocioId { get; init; }
        public Guid? GrupoProductoId { get; init; }
        public Guid? GrupoIvaNegocioId { get; init; }
        public Guid? GrupoIvaProductoId { get; init; }
        public short TipoOrigen { get; init; }
        public string ClaveOrigen { get; init; } = string.Empty;
    }
}
