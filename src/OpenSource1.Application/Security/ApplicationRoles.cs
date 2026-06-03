namespace OpenSource1.Application.Security;

public static class ApplicationRoles
{
    public const string Administrator = "Administrador";
    public const string Supervisor = "Supervisor";
    public const string Executor = "Ejecutor";

    public static readonly string[] All = [Administrator, Supervisor, Executor];
}
