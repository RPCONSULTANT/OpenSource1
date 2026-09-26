using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Blazor.Components;

/// <summary>Campos comunes (Código/Descripción) de los formularios de grupos contables, para <see cref="GrupoContableFields"/>.</summary>
public interface IGrupoContableCampos
{
    string Codigo { get; set; }
    string Descripcion { get; set; }
}

/// <summary>Formulario de alta/modificación de un grupo contable simple (página <c>/grupos-contables</c>).</summary>
public sealed class GrupoContableForm : IGrupoContableCampos
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [MaxLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [MaxLength(100, ErrorMessage = "La descripción no puede superar los 100 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    public long Xmin { get; set; }
}

/// <summary>Formulario de alta/modificación de un grupo contable de cliente (página <c>/grupos-cliente-contable</c>).</summary>
public sealed class GrupoClienteContableForm : IGrupoContableCampos
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [MaxLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [MaxLength(100, ErrorMessage = "La descripción no puede superar los 100 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    // "" (sin selección) llega como null desde el <select>.
    [Required(ErrorMessage = "Seleccione la cuenta por cobrar.")]
    public Guid? CuentaCxCId { get; set; }

    public Guid? CuentaDescuentoId { get; set; }

    public Guid? CuentaInteresId { get; set; }

    public long Xmin { get; set; }
}
