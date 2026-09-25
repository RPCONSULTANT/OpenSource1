using System.Globalization;
using ClosedXML.Excel;
using OpenSource1.Blazor.Components;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OpenSource1.Blazor.Reporting;

public sealed class QuestPdfReportDocumentService : IReportDocumentService
{
    public ReportFile GenerateClientesReport(IReadOnlyList<SocioNegocioResponse> clientes, string title)
    {
        var now = DateTimeOffset.Now;
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                // Apaisado: con los campos de facturación la tabla ya no cabe en vertical.
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.Header().Column(column =>
                {
                    column.Item().Text(title).FontSize(20).Bold().FontColor(Colors.Blue.Medium);
                    column.Item().Text($"Fecha de generación: {now:dd/MM/yyyy HH:mm}").FontSize(10);
                    column.Item().Text($"Cantidad total de registros: {clientes.Count}").FontSize(10).SemiBold();
                });

                page.Content().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(0.9f);  // Código
                        columns.RelativeColumn(2.0f);  // Nombre comercial
                        columns.RelativeColumn(1.1f);  // Tipo
                        columns.RelativeColumn(1.5f);  // Documento fiscal
                        columns.RelativeColumn(1.8f);  // Correo
                        columns.RelativeColumn(1.1f);  // Teléfono
                        columns.RelativeColumn(1.8f);  // Dirección
                        columns.RelativeColumn(1.1f);  // Límite de crédito
                        columns.RelativeColumn(1.2f);  // Bloqueo
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Código");
                        HeaderCell(header.Cell(), "Nombre comercial");
                        HeaderCell(header.Cell(), "Tipo");
                        HeaderCell(header.Cell(), "Documento fiscal");
                        HeaderCell(header.Cell(), "Correo");
                        HeaderCell(header.Cell(), "Teléfono");
                        HeaderCell(header.Cell(), "Dirección");
                        HeaderCell(header.Cell(), "Límite de crédito");
                        HeaderCell(header.Cell(), "Bloqueo");
                    });

                    foreach (var cliente in clientes)
                    {
                        BodyCell(table, cliente.Codigo);
                        BodyCell(table, cliente.NombreComercial);
                        BodyCell(table, SocioNegocioEtiquetas.Tipo(cliente.Tipo));
                        BodyCell(table, SocioNegocioEtiquetas.Documento(cliente.TipoDocumentoFiscal, cliente.NumeroDocumentoFiscal));
                        BodyCell(table, cliente.Email ?? "—");
                        BodyCell(table, cliente.Telefono ?? "—");
                        BodyCell(table, DireccionDisplay(cliente.DireccionLinea1, cliente.DireccionLinea2));
                        BodyCell(table, cliente.LimiteCredito.ToString("N2", CultureInfo.InvariantCulture));
                        BodyCell(table, cliente.Bloqueado == BloqueoSocioNegocio.Ninguno ? "—" : SocioNegocioEtiquetas.Bloqueo(cliente.Bloqueado));
                    }
                });

                page.Footer().AlignRight().DefaultTextStyle(x => x.FontSize(10)).Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                });
            });
        }).GeneratePdf();

        return new ReportFile($"clientes-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf", "application/pdf", pdf);
    }

    public ReportFile GenerateProductosReport(IReadOnlyList<ProductoResponse> productos, string title)
    {
        var now = DateTimeOffset.Now;
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Header().Column(column =>
                {
                    column.Item().Text(title).FontSize(20).Bold().FontColor(Colors.Blue.Medium);
                    column.Item().Text($"Fecha de generación: {now:dd/MM/yyyy HH:mm}").FontSize(10);
                    column.Item().Text($"Cantidad total de registros: {productos.Count}").FontSize(10).SemiBold();
                });

                page.Content().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.8f);
                        columns.RelativeColumn(1f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(1.4f);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Código");
                        HeaderCell(header.Cell(), "Nombre");
                        HeaderCell(header.Cell(), "Precio de venta");
                        HeaderCell(header.Cell(), "Stock");
                        HeaderCell(header.Cell(), "Categoría");
                    });

                    foreach (var producto in productos)
                    {
                        BodyCell(table, producto.Codigo);
                        BodyCell(table, producto.Nombre);
                        BodyCell(table, producto.PrecioVenta.ToString("N2"));
                        BodyCell(table, producto.Stock.ToString());
                        BodyCell(table, producto.CategoriaNombre);
                    }
                });

                page.Footer().AlignRight().DefaultTextStyle(x => x.FontSize(10)).Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                });
            });
        }).GeneratePdf();

        return new ReportFile($"productos-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf", "application/pdf", pdf);
    }

    public ReportFile GenerateClientesExcel(IReadOnlyList<SocioNegocioResponse> clientes, string title)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Clientes");

        string[] headers = ["Código", "Nombre comercial", "Tipo", "Documento fiscal", "Correo", "Teléfono", "Dirección", "Límite de crédito", "Bloqueo"];

        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, headers.Length).Merge().Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = $"Fecha de generación: {DateTimeOffset.Now:dd/MM/yyyy HH:mm}";
        sheet.Range(2, 1, 2, headers.Length).Merge();

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"));
        }

        var row = 5;
        foreach (var cliente in clientes)
        {
            // El código es texto (conserva los ceros a la izquierda: "000012").
            sheet.Cell(row, 1).SetValue(cliente.Codigo);
            sheet.Cell(row, 2).Value = cliente.NombreComercial;
            sheet.Cell(row, 3).Value = SocioNegocioEtiquetas.Tipo(cliente.Tipo);
            sheet.Cell(row, 4).Value = SocioNegocioEtiquetas.Documento(cliente.TipoDocumentoFiscal, cliente.NumeroDocumentoFiscal);
            sheet.Cell(row, 5).Value = cliente.Email ?? "—";
            sheet.Cell(row, 6).Value = cliente.Telefono ?? "—";
            sheet.Cell(row, 7).Value = DireccionDisplay(cliente.DireccionLinea1, cliente.DireccionLinea2);
            sheet.Cell(row, 8).Value = cliente.LimiteCredito;
            sheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 9).Value = cliente.Bloqueado == BloqueoSocioNegocio.Ninguno ? "—" : SocioNegocioEtiquetas.Bloqueo(cliente.Bloqueado);
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(
            $"clientes-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray());
    }

    public ReportFile GenerateProductosExcel(IReadOnlyList<ProductoResponse> productos, string title)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Productos");

        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, 5).Merge().Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = $"Fecha de generación: {DateTimeOffset.Now:dd/MM/yyyy HH:mm}";
        sheet.Range(2, 1, 2, 5).Merge();

        string[] headers = ["Código", "Nombre", "Precio de venta", "Stock", "Categoría"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"));
        }

        var row = 5;
        foreach (var producto in productos)
        {
            sheet.Cell(row, 1).Value = producto.Codigo;
            sheet.Cell(row, 2).Value = producto.Nombre;
            sheet.Cell(row, 3).Value = producto.PrecioVenta;
            sheet.Cell(row, 4).Value = producto.Stock;
            sheet.Cell(row, 5).Value = producto.CategoriaNombre;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(
            $"productos-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray());
    }

    public ReportFile GenerateClientesRawExcel(IReadOnlyList<SocioNegocioResponse> clientes)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Clientes");

        // Dato crudo (Power BI y otras herramientas): los enums van como código numérico y, al lado, su nombre.
        string[] headers =
        [
            "Id", "Codigo", "Tipo", "TipoNombre", "NombreComercial", "RazonSocial", "TipoDocumentoFiscal", "TipoDocumentoFiscalNombre",
            "NumeroDocumentoFiscal", "Email", "Telefono", "DireccionLinea1", "DireccionLinea2", "Ciudad", "Sector", "PaisCodigo", "PaisNombre",
            "TerminoPagoId", "LimiteCredito", "Bloqueado", "BloqueadoNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
        ];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"));
        }

        var row = 2;
        foreach (var cliente in clientes)
        {
            var col = 1;
            sheet.Cell(row, col++).Value = cliente.Id.ToString();
            sheet.Cell(row, col++).SetValue(cliente.Codigo);
            sheet.Cell(row, col++).Value = (int)cliente.Tipo;
            sheet.Cell(row, col++).Value = SocioNegocioEtiquetas.Tipo(cliente.Tipo);
            sheet.Cell(row, col++).Value = cliente.NombreComercial;
            sheet.Cell(row, col++).Value = cliente.RazonSocial ?? string.Empty;
            sheet.Cell(row, col++).Value = (int)cliente.TipoDocumentoFiscal;
            sheet.Cell(row, col++).Value = SocioNegocioEtiquetas.TipoDocumento(cliente.TipoDocumentoFiscal);
            sheet.Cell(row, col++).Value = cliente.NumeroDocumentoFiscal ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.Email ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.Telefono ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.DireccionLinea1 ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.DireccionLinea2 ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.Ciudad ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.Sector ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.PaisCodigo ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.PaisNombre ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.TerminoPagoId?.ToString() ?? string.Empty;
            sheet.Cell(row, col).Value = cliente.LimiteCredito;
            sheet.Cell(row, col++).Style.NumberFormat.Format = "0.0000";
            sheet.Cell(row, col++).Value = (int)cliente.Bloqueado;
            sheet.Cell(row, col++).Value = SocioNegocioEtiquetas.Bloqueo(cliente.Bloqueado);
            sheet.Cell(row, col++).Value = cliente.ImagePath ?? string.Empty;
            sheet.Cell(row, col++).Value = cliente.CreatedAtUtc;
            if (cliente.UpdatedAtUtc is { } updatedAt)
            {
                sheet.Cell(row, col).Value = updatedAt;
            }
            col++;
            sheet.Cell(row, col++).Value = cliente.CreatedBy;
            sheet.Cell(row, col).Value = cliente.UpdatedBy ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(
            $"clientes-crudo-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray());
    }

    public ReportFile GenerateProductosRawExcel(IReadOnlyList<ProductoResponse> productos)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Productos");

        // Dato crudo (Power BI y otras herramientas): los enums van como código numérico y, al lado, su nombre.
        string[] headers =
        [
            "Id", "Codigo", "Nombre", "PrecioVenta", "Stock", "CategoriaId", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaBaseId",
            "UnidadMedidaCodigo", "UnidadMedidaNombre", "MetodoCosteo", "MetodoCosteoNombre", "CostoUnitario", "CostoEstandar", "CostoAjustado",
            "Bloqueado", "BloqueadoNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
        ];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"));
        }

        var row = 2;
        foreach (var producto in productos)
        {
            var col = 1;
            sheet.Cell(row, col++).Value = producto.Id.ToString();
            sheet.Cell(row, col++).Value = producto.Codigo;
            sheet.Cell(row, col++).Value = producto.Nombre;
            sheet.Cell(row, col).Value = producto.PrecioVenta;
            sheet.Cell(row, col++).Style.NumberFormat.Format = "0.0000";
            sheet.Cell(row, col++).Value = producto.Stock;
            sheet.Cell(row, col++).Value = producto.CategoriaId.ToString();
            sheet.Cell(row, col++).Value = producto.CategoriaCodigo;
            sheet.Cell(row, col++).Value = producto.CategoriaNombre;
            sheet.Cell(row, col++).Value = producto.UnidadMedidaBaseId.ToString();
            sheet.Cell(row, col++).Value = producto.UnidadMedidaCodigo;
            sheet.Cell(row, col++).Value = producto.UnidadMedidaNombre;
            sheet.Cell(row, col++).Value = (int)producto.MetodoCosteo;
            sheet.Cell(row, col++).Value = ProductoEtiquetas.Metodo(producto.MetodoCosteo);
            sheet.Cell(row, col).Value = producto.CostoUnitario;
            sheet.Cell(row, col++).Style.NumberFormat.Format = "0.0000";
            sheet.Cell(row, col).Value = producto.CostoEstandar;
            sheet.Cell(row, col++).Style.NumberFormat.Format = "0.0000";
            sheet.Cell(row, col++).Value = producto.CostoAjustado;
            sheet.Cell(row, col++).Value = (int)producto.Bloqueado;
            sheet.Cell(row, col++).Value = ProductoEtiquetas.Bloqueo(producto.Bloqueado);
            sheet.Cell(row, col++).Value = producto.ImagePath ?? string.Empty;
            sheet.Cell(row, col++).Value = producto.CreatedAtUtc;
            if (producto.UpdatedAtUtc is { } updatedAt)
            {
                sheet.Cell(row, col).Value = updatedAt;
            }
            col++;
            sheet.Cell(row, col++).Value = producto.CreatedBy;
            sheet.Cell(row, col).Value = producto.UpdatedBy ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(
            $"productos-crudo-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray());
    }

    private static string DireccionDisplay(string? linea1, string? linea2)
    {
        if (string.IsNullOrWhiteSpace(linea1))
        {
            return "—";
        }

        return string.IsNullOrWhiteSpace(linea2) ? linea1 : $"{linea1}, {linea2}";
    }

    private static void HeaderCell(IContainer container, string text)
    {
        container.Background(Colors.Grey.Lighten3).Padding(6).Text(text).Bold().FontSize(10);
    }

    private static void BodyCell(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Text(text).FontSize(9);
    }
}
