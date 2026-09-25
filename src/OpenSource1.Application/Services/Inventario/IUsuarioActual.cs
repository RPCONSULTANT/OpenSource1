namespace OpenSource1.Application.Services.Inventario;

/// <summary>
/// Usuario en cuyo nombre se escribe el libro (<c>CreatedBy</c>/<c>UsuarioId</c>). En la API sale de los claims del
/// JWT; fuera de una petición HTTP (tests, procesos batch) vale <c>"system"</c> sin id.
/// </summary>
public interface IUsuarioActual
{
    string Nombre { get; }
    Guid? Id { get; }
}
