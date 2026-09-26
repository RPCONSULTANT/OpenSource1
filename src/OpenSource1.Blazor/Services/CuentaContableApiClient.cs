using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class CuentaContableApiClient(HttpClient httpClient, ILogger<CuentaContableApiClient> logger) : ICuentaContableApiClient
{
    public async Task<PagedResult<CuentaContableResponse>> ListAsync(CuentaContableSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter, paginacion), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("CuentasContables LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener las cuentas contables.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<CuentaContableResponse>>(cancellationToken)
            ?? PagedResult<CuentaContableResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<CuentaContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/cuentas-contables/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("CuentasContables GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener la cuenta contable.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<CuentaContableResponse>(cancellationToken);
    }

    private static string BuildListUrl(CuentaContableSearchFilter? filter, PageRequest? paginacion)
    {
        var parameters = new List<string>();

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Numero))
            {
                parameters.Add($"numero={Uri.EscapeDataString(filter.Numero.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Nombre))
            {
                parameters.Add($"nombre={Uri.EscapeDataString(filter.Nombre.Trim())}");
            }

            if (filter.TipoCuenta is { } tipoCuenta)
            {
                parameters.Add($"tipoCuenta={(int)tipoCuenta}");
            }

            if (filter.TipoResultado is { } tipoResultado)
            {
                parameters.Add($"tipoResultado={(int)tipoResultado}");
            }

            if (filter.Bloqueada is { } bloqueada)
            {
                parameters.Add($"bloqueada={(bloqueada ? "true" : "false")}");
            }
        }

        AddPaginationParameters(parameters, paginacion);

        return parameters.Count == 0 ? "api/cuentas-contables" : $"api/cuentas-contables?{string.Join("&", parameters)}";
    }

    private static void AddPaginationParameters(List<string> parameters, PageRequest? paginacion)
    {
        if (paginacion is null)
        {
            return;
        }

        parameters.Add($"pagina={paginacion.Pagina}");
        parameters.Add($"tamanoPagina={paginacion.TamanoPagina}");

        if (!string.IsNullOrWhiteSpace(paginacion.OrdenarPor))
        {
            parameters.Add($"ordenarPor={Uri.EscapeDataString(paginacion.OrdenarPor)}");
        }

        parameters.Add($"descendente={(paginacion.Descendente ? "true" : "false")}");
    }

    public async Task<CuentaContableOperationResult> CreateAsync(CuentaContableInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/cuentas-contables", input, cancellationToken);
        return await ToResultAsync(response, "Cuenta contable agregada correctamente.", cancellationToken);
    }

    public async Task<CuentaContableOperationResult> UpdateAsync(Guid id, CuentaContableInput input, long xmin, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            input.Numero, input.Nombre, input.TipoCuenta, input.TipoResultado,
            PosteoDirecto = (bool?)input.PosteoDirecto, Bloqueada = (bool?)input.Bloqueada, Sangria = (int?)input.Sangria,
            Xmin = xmin
        };
        using var response = await httpClient.PutAsJsonAsync($"api/cuentas-contables/{id}", body, cancellationToken);
        return await ToResultAsync(response, "Cuenta contable modificada correctamente.", cancellationToken);
    }

    public async Task<CuentaContableOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/cuentas-contables/{id}", cancellationToken);
        return await ToResultAsync(response, "Cuenta contable eliminada correctamente.", cancellationToken);
    }

    private async Task<CuentaContableOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<CuentaContableResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch
            {
                // ignore when response has no DTO body (delete/no-content)
            }

            return new CuentaContableOperationResult(true, successMessage, entityId);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("CuentasContables API returned {StatusCode}. Body: {Body}", response.StatusCode, body);

        // Los 400/409 de la API traen mensajes de validación propios (texto seguro, en español): se
        // muestran tal cual. Si no se pueden leer, se cae al mensaje genérico de cada estado.
        var mensajes = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict
            ? ExtraerMensajes(body)
            : [];

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró la cuenta contable indicada.",
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los datos del formulario:" : "Revise los datos del formulario.",
            // El 409 puede venir de un Result.Fallo del handler (cuenta_contable.conflicto, número duplicado, o el
            // manejador global de una violación de índice único; también de un Xmin desactualizado, que da
            // entidad.modificada_por_otro) — ExtraerMensajes cubre ambas formas de cuerpo.
            HttpStatusCode.Conflict => mensajes.Count > 0 ? string.Join(" ", mensajes) : "La operación entra en conflicto con otro registro (número duplicado, cuenta en uso o modificada por otro usuario).",
            _ => "No fue posible completar la operación."
        };

        // En un 409 el mensaje ya es la lista completa; en un 400 la lista se muestra aparte, por campo.
        var errores = response.StatusCode == HttpStatusCode.BadRequest ? mensajes : [];
        return new CuentaContableOperationResult(false, safe, Errors: errores);
    }

    /// <summary>
    /// Mensajes de un cuerpo de error de la API: el <c>ValidationProblemDetails</c> estándar
    /// (<c>errors</c> = objeto campo -> mensajes) o la lista plana de errores de binding
    /// (<c>errors</c> = array de textos), tal como los produce <c>ResultExtensions.ToActionResult</c>.
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
}
