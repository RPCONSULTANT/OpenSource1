namespace OpenSource1.Core.Entities;

/// <summary>
/// Configuración general de las fechas de registro permitidas (Task 8.5, estilo "Allow Posting From/To" del General Ledger
/// Setup): fila ÚNICA sembrada con el Id fijo <see cref="ConfiguracionRegistroIds.General"/>, sin alta ni borrado. Un límite
/// <see langword="null"/> es "sin límite" por ese lado; la fila sembrada no tiene límites. Solo la modifica el Administrador.
/// </summary>
public sealed class ConfiguracionRegistro : BaseEntity
{
    public DateOnly? PermitirRegistroDesde { get; set; }

    public DateOnly? PermitirRegistroHasta { get; set; }
}

/// <summary>
/// Excepción por usuario a las fechas de registro permitidas (Task 8.5, estilo "User Setup"): si el usuario tiene fila propia
/// manda SOLO su rango (aunque sea más amplio o más estrecho que el general). <see cref="UsuarioId"/> es una referencia
/// lógica a la base de Identity (otra base de datos, sin FK) y es único entre las filas vivas; <see cref="NombreUsuario"/>
/// está desnormalizado para mostrarlo sin consultar Identity.
/// </summary>
public sealed class ConfiguracionRegistroUsuario : BaseEntity
{
    public Guid UsuarioId { get; set; }

    public required string NombreUsuario { get; set; }

    public DateOnly? PermitirRegistroDesde { get; set; }

    public DateOnly? PermitirRegistroHasta { get; set; }
}

/// <summary>Id fijo de la fila única sembrada de <see cref="ConfiguracionRegistro"/>.</summary>
public static class ConfiguracionRegistroIds
{
    public static readonly Guid General = Guid.Parse("f8000000-0000-0000-0000-000000000001");
}
