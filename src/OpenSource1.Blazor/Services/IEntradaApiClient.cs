using OpenSource1.Application.Features.Entradas.Dtos;
using System;

namespace OpenSource1.Blazor.Services;

[Obsolete("Modulo de prueba obsoleto. No usar Entradas para nuevos desarrollos.")]
public interface IEntradaApiClient
{
    Task<IReadOnlyList<EntradaResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<EntradaOperationResult> CreateAsync(EntradaInput input, CancellationToken cancellationToken = default);
    Task<EntradaOperationResult> UpdateAsync(Guid id, EntradaInput input, CancellationToken cancellationToken = default);
    Task<EntradaOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

[Obsolete("Modulo de prueba obsoleto. No usar Entradas para nuevos desarrollos.")]
public sealed record EntradaInput(string Titulo, string? Descripcion, string Tipo, string Estado);
[Obsolete("Modulo de prueba obsoleto. No usar Entradas para nuevos desarrollos.")]
public sealed record EntradaOperationResult(bool Succeeded, string Message);
