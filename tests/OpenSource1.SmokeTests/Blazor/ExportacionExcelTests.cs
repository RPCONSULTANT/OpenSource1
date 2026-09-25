extern alias BlazorApp;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BlazorApp::OpenSource1.Blazor.Reporting;
using ClosedXML.Excel;
using OpenSource1.Application.Features.Productos.Dtos;
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

    private static ProductoResponse Producto(string texto) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = "P-1",
        Nombre = texto,
        PrecioVenta = 12.3456m,
        Stock = 7,
        CategoriaId = Guid.NewGuid(),
        CategoriaCodigo = texto,
        CategoriaNombre = texto,
        UnidadMedidaBaseId = Guid.NewGuid(),
        UnidadMedidaCodigo = texto,
        UnidadMedidaNombre = texto,
        CostoEstandar = 3m,
        Bloqueado = BloqueoProducto.Venta,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = texto,
    };

    [Theory]
    [MemberData(nameof(Textos))]
    public void ProductosExcel_TextoConAspectoDeFormula_NoGeneraFormulaYLaCeldaEsTexto(string texto)
    {
        var servicio = new QuestPdfReportDocumentService();

        AssertSinFormulas(servicio.GenerateProductosExcel([Producto(texto)], "Reporte").Content, texto);
        AssertSinFormulas(servicio.GenerateProductosRawExcel([Producto(texto)]).Content, texto);
    }

    [Fact]
    public void ProductosExcelCrudo_TraePrecioVentaYLosCamposNuevos_ConSusValores()
    {
        var servicio = new QuestPdfReportDocumentService();
        var producto = Producto("Mesa");

        using var libro = new XLWorkbook(new MemoryStream(servicio.GenerateProductosRawExcel([producto]).Content));
        var hoja = libro.Worksheet(1);
        var cabeceras = hoja.Row(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains("PrecioVenta", cabeceras);
        Assert.DoesNotContain("Precio", cabeceras);
        foreach (var esperada in new[] { "Stock", "CategoriaId", "CategoriaCodigo", "UnidadMedidaBaseId", "UnidadMedidaCodigo", "MetodoCosteo", "CostoUnitario", "CostoEstandar", "CostoAjustado", "Bloqueado", "BloqueadoNombre" })
        {
            Assert.Contains(esperada, cabeceras);
        }

        var fila = hoja.Row(2);
        Assert.Equal(12.3456m, fila.Cell(cabeceras.IndexOf("PrecioVenta") + 1).GetValue<decimal>());
        Assert.Equal(7, fila.Cell(cabeceras.IndexOf("Stock") + 1).GetValue<int>());
        Assert.Equal(1, fila.Cell(cabeceras.IndexOf("Bloqueado") + 1).GetValue<int>());
        Assert.Equal("Bloqueado para la venta", fila.Cell(cabeceras.IndexOf("BloqueadoNombre") + 1).GetString());
        Assert.True(fila.Cell(cabeceras.IndexOf("CostoAjustado") + 1).GetValue<bool>());
    }

    [Fact]
    public void ProductosExcel_TraeElPrecioDeVentaEnLaTercerColumna()
    {
        var servicio = new QuestPdfReportDocumentService();

        using var libro = new XLWorkbook(new MemoryStream(servicio.GenerateProductosExcel([Producto("Mesa")], "Reporte").Content));
        var hoja = libro.Worksheet(1);

        Assert.Equal("Precio de venta", hoja.Cell(4, 3).GetString());
        Assert.Equal(12.3456m, hoja.Cell(5, 3).GetValue<decimal>());
    }
}
