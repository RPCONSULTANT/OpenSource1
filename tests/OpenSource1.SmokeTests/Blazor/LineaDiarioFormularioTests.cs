extern alias BlazorApp;
using System.ComponentModel.DataAnnotations;
using BlazorApp::OpenSource1.Blazor.Components.Pages;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// <see cref="DiarioInventarioLote.LineaForm"/>: mismo patrón que <c>ProductoFormularioTests</c>
/// (Productos) — valida el parseo de texto (fecha/cantidad/costo, independiente de la cultura del
/// servidor) sin necesitar un HttpContext ni renderizar la página completa.
/// </summary>
public sealed class LineaDiarioFormularioTests
{
    private static List<ValidationResult> Validar(DiarioInventarioLote.LineaForm form)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), resultados, validateAllProperties: true);
        return resultados;
    }

    private static DiarioInventarioLote.LineaForm FormularioValido() => new()
    {
        FechaRegistroTexto = "2026-09-25",
        FechaDocumentoTexto = "2026-09-25",
        AlmacenId = Guid.NewGuid(),
        CantidadTexto = "100",
        CostoUnitarioTexto = "10.00",
    };

    [Fact]
    public void Formulario_ConDatosValidos_NoTieneErrores_YExponeLosValoresInterpretados()
    {
        var form = FormularioValido();

        Assert.Empty(Validar(form));
        Assert.Equal(new DateOnly(2026, 9, 25), form.FechaRegistro);
        Assert.Equal(new DateOnly(2026, 9, 25), form.FechaDocumento);
        Assert.Equal(100m, form.Cantidad);
        Assert.Equal(10.00m, form.CostoUnitario);
    }

    [Fact]
    public void Formulario_SinFechas_SeñalaAmbosCamposComoObligatorios()
    {
        var form = FormularioValido();
        form.FechaRegistroTexto = "";
        form.FechaDocumentoTexto = "";

        var errores = Validar(form);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(DiarioInventarioLote.LineaForm.FechaRegistroTexto)));
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(DiarioInventarioLote.LineaForm.FechaDocumentoTexto)));
    }

    [Theory]
    [InlineData("25/09/2026")]
    [InlineData("no es una fecha")]
    public void Formulario_ConFechaEnFormatoDistintoAIso_SeñalaElCampoConUnMensaje(string texto)
    {
        var form = FormularioValido();
        form.FechaRegistroTexto = texto;

        var error = Assert.Single(Validar(form));
        Assert.Contains(nameof(DiarioInventarioLote.LineaForm.FechaRegistroTexto), error.MemberNames);
    }

    [Fact]
    public void Formulario_SinCantidad_LaSeñalaComoObligatoria()
    {
        var form = FormularioValido();
        form.CantidadTexto = "";

        var errores = Validar(form);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(DiarioInventarioLote.LineaForm.CantidadTexto)));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,500")]
    public void Formulario_ConCantidadInvalida_SeñalaElCampo(string texto)
    {
        var form = FormularioValido();
        form.CantidadTexto = texto;

        var error = Assert.Single(Validar(form));
        Assert.Contains(nameof(DiarioInventarioLote.LineaForm.CantidadTexto), error.MemberNames);
    }

    [Fact]
    public void Formulario_ConCantidadCeroONegativa_SeñalaElCampo()
    {
        var form = FormularioValido();
        form.CantidadTexto = "0";

        var error = Assert.Single(Validar(form));
        Assert.Contains(nameof(DiarioInventarioLote.LineaForm.CantidadTexto), error.MemberNames);
        Assert.Equal("La cantidad debe ser mayor que cero.", error.ErrorMessage);
    }

    [Fact]
    public void Formulario_CostoUnitarioVacio_NoEsError_YCostoUnitarioEsNull()
    {
        var form = FormularioValido();
        form.CostoUnitarioTexto = null;

        Assert.Empty(Validar(form));
        Assert.Null(form.CostoUnitario);
    }

    [Fact]
    public void Formulario_ConCostoUnitarioInvalido_SeñalaElCampo()
    {
        var form = FormularioValido();
        form.CostoUnitarioTexto = "1,500";

        var error = Assert.Single(Validar(form));
        Assert.Contains(nameof(DiarioInventarioLote.LineaForm.CostoUnitarioTexto), error.MemberNames);
    }
}
