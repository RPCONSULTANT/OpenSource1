using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Registros.Commands;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class DiarioInventarioApiClient(HttpClient httpClient, ILogger<DiarioInventarioApiClient> logger) : IDiarioInventarioApiClient
{
    private const string BaseRoute = "api/diarios-inventario";

    public async Task<IReadOnlyList<PlantillaDiarioResponse>> ListPlantillasAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BaseRoute}/plantillas", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Plantillas de diario LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener las plantillas de diario.", inner: null, statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<List<PlantillaDiarioResponse>>(cancellationToken) ?? [];
    }

    public async Task<PagedResult<LoteDiarioResponse>> ListLotesAsync(
        LoteDiarioSearchCriteria? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildLotesListUrl(filter, paginacion), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Lotes de diario LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener los lotes de diario.", inner: null, statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<LoteDiarioResponse>>(cancellationToken)
            ?? PagedResult<LoteDiarioResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<LoteDiarioResponse?> GetLoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BaseRoute}/lotes/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Lote de diario GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener el lote de diario.", inner: null, statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<LoteDiarioResponse>(cancellationToken);
    }

    public async Task<LoteDiarioOperationResult> CreateLoteAsync(LoteDiarioInput input, CancellationToken cancellationToken = default)
    {
        var body = new LoteCreateBody(input.PlantillaDiarioId, input.Codigo, input.Nombre, SerieId: null, input.Bloqueado);
        using var response = await httpClient.PostAsJsonAsync($"{BaseRoute}/lotes", body, cancellationToken);
        return await ToLoteResultAsync(response, "Lote de diario agregado correctamente.", cancellationToken);
    }

    public async Task<LoteDiarioOperationResult> UpdateLoteAsync(Guid id, LoteDiarioInput input, long xmin, CancellationToken cancellationToken = default)
    {
        var body = new LoteUpdateBody(input.Codigo, input.Nombre, SerieId: null, input.Bloqueado, xmin);
        using var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/lotes/{id}", body, cancellationToken);
        return await ToLoteResultAsync(response, "Lote de diario modificado correctamente.", cancellationToken);
    }

    public async Task<DiarioOperationResult> DeleteLoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{BaseRoute}/lotes/{id}", cancellationToken);
        return await ToPlainResultAsync(response, "Lote de diario eliminado correctamente.", cancellationToken);
    }

    public async Task<RegistrarLoteOperationResult> RegistrarLoteAsync(Guid loteId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"{BaseRoute}/lotes/{loteId}/registrar", content: null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<ResultadoRegistroLote>(cancellationToken);
            return new RegistrarLoteOperationResult(true, "Lote registrado correctamente.", payload);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Registrar lote de diario returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        var (safe, errores) = InterpretarError(response.StatusCode, body, "No se encontró el lote de diario indicado.");
        return new RegistrarLoteOperationResult(false, safe, Errors: errores);
    }

    public async Task<IReadOnlyList<LineaDiarioResponse>?> ListLineasAsync(Guid loteId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BaseRoute}/lotes/{loteId}/lineas", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Líneas de diario LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener las líneas del lote.", inner: null, statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<List<LineaDiarioResponse>>(cancellationToken) ?? [];
    }

    public async Task<LineaDiarioResponse?> GetLineaByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BaseRoute}/lineas/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Línea de diario GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener la línea de diario.", inner: null, statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<LineaDiarioResponse>(cancellationToken);
    }

    public async Task<LineaDiarioOperationResult> CreateLineaAsync(Guid loteId, LineaDiarioInput input, CancellationToken cancellationToken = default)
    {
        var body = new LineaBody(
            input.FechaRegistro, input.FechaDocumento, input.NumeroDocumento, input.TipoMovimiento, input.ProductoId,
            input.AlmacenId, input.AlmacenDestinoId, input.UnidadMedidaId, input.Cantidad, input.CostoUnitario, input.Descripcion);
        using var response = await httpClient.PostAsJsonAsync($"{BaseRoute}/lotes/{loteId}/lineas", body, cancellationToken);
        return await ToLineaResultAsync(response, "Línea agregada correctamente.", cancellationToken);
    }

    public async Task<LineaDiarioOperationResult> UpdateLineaAsync(Guid id, LineaDiarioInput input, long xmin, CancellationToken cancellationToken = default)
    {
        var body = new LineaUpdateBody(
            input.FechaRegistro, input.FechaDocumento, input.NumeroDocumento, input.TipoMovimiento, input.ProductoId,
            input.AlmacenId, input.AlmacenDestinoId, input.UnidadMedidaId, input.Cantidad, input.CostoUnitario, input.Descripcion, xmin);
        using var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/lineas/{id}", body, cancellationToken);
        return await ToLineaResultAsync(response, "Línea modificada correctamente.", cancellationToken);
    }

    public async Task<DiarioOperationResult> DeleteLineaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{BaseRoute}/lineas/{id}", cancellationToken);
        return await ToPlainResultAsync(response, "Línea eliminada correctamente.", cancellationToken);
    }

    // ───────────────────────── helpers ─────────────────────────

    private static string BuildLotesListUrl(LoteDiarioSearchCriteria? filter, PageRequest? paginacion)
    {
        var parameters = new List<string>();

        if (filter is not null)
        {
            if (filter.PlantillaDiarioId is { } plantillaId)
            {
                parameters.Add($"plantillaId={plantillaId}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Codigo))
            {
                parameters.Add($"codigo={Uri.EscapeDataString(filter.Codigo.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Nombre))
            {
                parameters.Add($"nombre={Uri.EscapeDataString(filter.Nombre.Trim())}");
            }
        }

        if (paginacion is not null)
        {
            parameters.Add($"pagina={paginacion.Pagina}");
            parameters.Add($"tamanoPagina={paginacion.TamanoPagina}");
            if (!string.IsNullOrWhiteSpace(paginacion.OrdenarPor))
            {
                parameters.Add($"ordenarPor={Uri.EscapeDataString(paginacion.OrdenarPor)}");
            }

            parameters.Add($"descendente={(paginacion.Descendente ? "true" : "false")}");
        }

        return parameters.Count == 0 ? $"{BaseRoute}/lotes" : $"{BaseRoute}/lotes?{string.Join("&", parameters)}";
    }

    private async Task<LoteDiarioOperationResult> ToLoteResultAsync(HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<LoteDiarioResponse>(cancellationToken);
            return new LoteDiarioOperationResult(true, successMessage, payload);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Lotes de diario API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        var (safe, errores) = InterpretarError(response.StatusCode, body, "No se encontró el lote de diario indicado.");
        return new LoteDiarioOperationResult(false, safe, Errors: errores);
    }

    private async Task<LineaDiarioOperationResult> ToLineaResultAsync(HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<LineaDiarioResponse>(cancellationToken);
            return new LineaDiarioOperationResult(true, successMessage, payload);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Líneas de diario API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        var (safe, errores) = InterpretarError(response.StatusCode, body, "No se encontró la línea de diario indicada.");
        return new LineaDiarioOperationResult(false, safe, Errors: errores);
    }

    private async Task<DiarioOperationResult> ToPlainResultAsync(HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return new DiarioOperationResult(true, successMessage);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Diarios de inventario API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        var (safe, errores) = InterpretarError(response.StatusCode, body, "No se encontró el registro indicado.");
        return new DiarioOperationResult(false, safe, errores);
    }

    /// <summary>
    /// Mensaje seguro + lista de errores por código de estado. 400/409/422 traen mensajes reales de la API (ver
    /// <see cref="ExtraerMensajes"/>); los demás usan un mensaje genérico. Solo el 400 separa la lista de errores
    /// (por campo, con el número de línea YA incluido en el texto, ver <c>PostearLoteDiarioCommandHandler.DeLinea</c>);
    /// en 409/422 el mensaje resumido ya es la lista completa.
    /// </summary>
    private static (string Safe, List<string> Errores) InterpretarError(HttpStatusCode statusCode, string body, string notFoundMessage)
    {
        var mensajes = statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity
            ? ExtraerMensajes(body)
            : [];

        var safe = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => notFoundMessage,
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los datos del formulario:" : "Revise los datos del formulario.",
            HttpStatusCode.Conflict => mensajes.Count > 0 ? string.Join(" ", mensajes) : "La operación entra en conflicto con otro registro (código duplicado, concurrencia o líneas existentes).",
            HttpStatusCode.UnprocessableEntity => mensajes.Count > 0 ? string.Join(" ", mensajes) : "El lote está bloqueado.",
            _ => "No fue posible completar la operación."
        };

        var errores = statusCode == HttpStatusCode.BadRequest ? mensajes : [];
        return (safe, errores);
    }

    /// <summary>
    /// Mensajes de un cuerpo de error de la API: el <c>ValidationProblemDetails</c> estándar (<c>errors</c> = objeto
    /// campo -> mensajes, incluye "Lineas[n].Campo" con el número de línea YA dentro del texto del mensaje) o el
    /// <c>ProblemDetails</c> simple del <c>GlobalExceptionHandler</c> (sin "errors", solo "title": concurrencia,
    /// código duplicado, deadlock). Mismo patrón que <c>AlmacenApiClient</c>. Sin duplicados; vacío si no aplica.
    /// </summary>
    private static List<string> ExtraerMensajes(string body)
    {
        var mensajes = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("errors", out var errores))
            {
                if (doc.RootElement.TryGetProperty("title", out var titulo) && titulo.ValueKind == JsonValueKind.String)
                {
                    var texto = titulo.GetString();
                    if (!string.IsNullOrWhiteSpace(texto))
                    {
                        mensajes.Add(texto);
                    }
                }

                return mensajes;
            }

            var valores = new List<JsonElement>();
            if (errores.ValueKind == JsonValueKind.Object)
            {
                foreach (var campo in errores.EnumerateObject())
                {
                    if (campo.Value.ValueKind == JsonValueKind.Array)
                    {
                        valores.AddRange(campo.Value.EnumerateArray());
                    }
                    else
                    {
                        valores.Add(campo.Value);
                    }
                }
            }
            else if (errores.ValueKind == JsonValueKind.Array)
            {
                valores.AddRange(errores.EnumerateArray());
            }

            foreach (var valor in valores)
            {
                var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
                if (!string.IsNullOrWhiteSpace(texto) && !mensajes.Contains(texto))
                {
                    mensajes.Add(texto);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            mensajes.Clear();
        }

        return mensajes;
    }

    // Cuerpos de red exactos de DiariosInventarioController (Api project): no se referencia el proyecto Api desde
    // Blazor, así que se repiten aquí (mismo patrón que *Input en AlmacenApiClient/ProductoApiClient).
    private sealed record LoteCreateBody(Guid PlantillaDiarioId, string Codigo, string Nombre, Guid? SerieId, bool Bloqueado);

    private sealed record LoteUpdateBody(string Codigo, string Nombre, Guid? SerieId, bool? Bloqueado, long Xmin);

    private sealed record LineaBody(
        DateOnly FechaRegistro, DateOnly FechaDocumento, string? NumeroDocumento,
        OpenSource1.Core.Enums.TipoMovimientoInventario TipoMovimiento, Guid ProductoId, Guid AlmacenId,
        Guid? AlmacenDestinoId, Guid UnidadMedidaId, decimal Cantidad, decimal? CostoUnitario, string? Descripcion);

    private sealed record LineaUpdateBody(
        DateOnly FechaRegistro, DateOnly FechaDocumento, string? NumeroDocumento,
        OpenSource1.Core.Enums.TipoMovimientoInventario TipoMovimiento, Guid ProductoId, Guid AlmacenId,
        Guid? AlmacenDestinoId, Guid UnidadMedidaId, decimal Cantidad, decimal? CostoUnitario, string? Descripcion, long Xmin);
}
