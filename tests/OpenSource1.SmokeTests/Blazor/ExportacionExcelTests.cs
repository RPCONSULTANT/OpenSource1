extern alias BlazorApp;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BlazorApp::OpenSource1.Blazor.Reporting;
using ClosedXML.Excel;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Inyección de fórmulas: un texto que empieza por <c>=</c>, <c>+</c>, <c>-</c> o <c>@</c> no debe acabar como fórmula en el
/// .xlsx exportado (se ejecutaría al abrirlo). Depende de la semántica de ClosedXML 0.105: si una actualización la cambiara,
/// este test falla.
/// </summary>
public sealed class ExportacionExcelTests
{
    public static TheoryData<string> Textos => new()
    {
        "=1+1",
        "+1+1",
        "-2+3",
        "@SUM(A1)",
        "=HYPERLINK(\"http://x\",\"y\")",
        "=cmd|' /C calc'!A0",
    };

    private static SocioNegocioResponse Socio(string texto) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = "000123",
        Tipo = TipoSocioNegocio.Cliente,
        NombreComercial = texto,
        RazonSocial = texto,
        TipoDocumentoFiscal = TipoDocumentoFiscal.Rnc,
        NumeroDocumentoFiscal = texto,
        Email = texto,
        Telefono = texto,
        DireccionLinea1 = texto,
        DireccionLinea2 = texto,
        Ciudad = texto,
        Sector = texto,
        LimiteCredito = 10m,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = texto,
    };

    [Theory]
    [MemberData(nameof(Textos))]
    public void ReporteExcel_TextoConAspectoDeFormula_NoGeneraFormulaYLaCeldaEsTexto(string texto)
    {
        var servicio = new QuestPdfReportDocumentService();
        var archivo = servicio.GenerateClientesExcel([Socio(texto)], "Reporte");

        AssertSinFormulas(archivo.Content, texto);
    }

    [Theory]
    [MemberData(nameof(Textos))]
    public void ExcelCrudo_TextoConAspectoDeFormula_NoGeneraFormulaYLaCeldaEsTexto(string texto)
    {
        var servicio = new QuestPdfReportDocumentService();
        var archivo = servicio.GenerateClientesRawExcel([Socio(texto)]);

        AssertSinFormulas(archivo.Content, texto);
    }

    private static void AssertSinFormulas(byte[] contenido, string texto)
    {
        using var zip = new ZipArchive(new MemoryStream(contenido), ZipArchiveMode.Read);
        foreach (var entrada in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var lector = new StreamReader(entrada.Open());
            var xml = lector.ReadToEnd();
            Assert.False(Regex.IsMatch(xml, "<f[ >/]"), $"{entrada.FullName} contiene una etiqueta <f> para '{texto}'");
        }

        using var libro = new XLWorkbook(new MemoryStream(contenido));
        var hoja = libro.Worksheet(1);
        var celdasConTexto = hoja.CellsUsed().Where(c => c.Value.IsText && c.GetString() == texto).ToList();
        Assert.NotEmpty(celdasConTexto);
        Assert.All(hoja.CellsUsed(), c => Assert.False(c.HasFormula, $"{c.Address} tiene fórmula"));
        Assert.All(celdasConTexto, c => Assert.Equal(XLDataType.Text, c.DataType));
    }
}
