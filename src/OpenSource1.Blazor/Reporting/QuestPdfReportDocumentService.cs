using ClosedXML.Excel;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OpenSource1.Blazor.Reporting;

public sealed class QuestPdfReportDocumentService : IReportDocumentService
{
    public ReportFile GenerateClientesReport(IReadOnlyList<ClienteResponse> clientes, string title)
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
                    column.Item().Text($"Cantidad total de registros: {clientes.Count}").FontSize(10).SemiBold();
                });

                page.Content().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.7f);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Nombre");
                        HeaderCell(header.Cell(), "Apellido");
                        HeaderCell(header.Cell(), "Correo");
                        HeaderCell(header.Cell(), "Teléfono");
                        HeaderCell(header.Cell(), "Dirección");
                    });

                    foreach (var cliente in clientes)
                    {
                        BodyCell(table, cliente.Nombre);
                        BodyCell(table, cliente.Apellido);
                        BodyCell(table, cliente.Email);
                        BodyCell(table, cliente.Telefono ?? "—");
                        BodyCell(table, cliente.Direccion ?? "—");
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
                        HeaderCell(header.Cell(), "Precio");
                        HeaderCell(header.Cell(), "Stock");
                        HeaderCell(header.Cell(), "Categoría");
                    });

                    foreach (var producto in productos)
                    {
                        BodyCell(table, producto.Codigo);
                        BodyCell(table, producto.Nombre);
                        BodyCell(table, producto.Precio.ToString("N2"));
                        BodyCell(table, producto.Stock.ToString());
                        BodyCell(table, producto.Categoria);
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

    public ReportFile GenerateClientesExcel(IReadOnlyList<ClienteResponse> clientes, string title)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Clientes");

        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, 5).Merge().Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = $"Fecha de generación: {DateTimeOffset.Now:dd/MM/yyyy HH:mm}";
        sheet.Range(2, 1, 2, 5).Merge();

        string[] headers = ["Nombre", "Apellido", "Correo", "Teléfono", "Dirección"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"));
        }

        var row = 5;
        foreach (var cliente in clientes)
        {
            sheet.Cell(row, 1).Value = cliente.Nombre;
            sheet.Cell(row, 2).Value = cliente.Apellido;
            sheet.Cell(row, 3).Value = cliente.Email;
            sheet.Cell(row, 4).Value = cliente.Telefono ?? "—";
            sheet.Cell(row, 5).Value = cliente.Direccion ?? "—";
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

        string[] headers = ["Código", "Nombre", "Precio", "Stock", "Categoría"];
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
            sheet.Cell(row, 3).Value = producto.Precio;
            sheet.Cell(row, 4).Value = producto.Stock;
            sheet.Cell(row, 5).Value = producto.Categoria;
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

    public ReportFile GenerateClientesRawExcel(IReadOnlyList<ClienteResponse> clientes)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Clientes");

        string[] headers = ["Id", "Nombre", "Apellido", "Email", "Telefono", "Direccion", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"));
        }

        var row = 2;
        foreach (var cliente in clientes)
        {
            sheet.Cell(row, 1).Value = cliente.Id.ToString();
            sheet.Cell(row, 2).Value = cliente.Nombre;
            sheet.Cell(row, 3).Value = cliente.Apellido;
            sheet.Cell(row, 4).Value = cliente.Email;
            sheet.Cell(row, 5).Value = cliente.Telefono ?? string.Empty;
            sheet.Cell(row, 6).Value = cliente.Direccion ?? string.Empty;
            sheet.Cell(row, 7).Value = cliente.ImagePath ?? string.Empty;
            sheet.Cell(row, 8).Value = cliente.CreatedAtUtc;
            if (cliente.UpdatedAtUtc is { } updatedAt)
            {
                sheet.Cell(row, 9).Value = updatedAt;
            }
            sheet.Cell(row, 10).Value = cliente.CreatedBy;
            sheet.Cell(row, 11).Value = cliente.UpdatedBy ?? string.Empty;
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

        string[] headers = ["Id", "Codigo", "Nombre", "Precio", "Stock", "Categoria", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"));
        }

        var row = 2;
        foreach (var producto in productos)
        {
            sheet.Cell(row, 1).Value = producto.Id.ToString();
            sheet.Cell(row, 2).Value = producto.Codigo;
            sheet.Cell(row, 3).Value = producto.Nombre;
            sheet.Cell(row, 4).Value = producto.Precio;
            sheet.Cell(row, 5).Value = producto.Stock;
            sheet.Cell(row, 6).Value = producto.Categoria;
            sheet.Cell(row, 7).Value = producto.ImagePath ?? string.Empty;
            sheet.Cell(row, 8).Value = producto.CreatedAtUtc;
            if (producto.UpdatedAtUtc is { } updatedAt)
            {
                sheet.Cell(row, 9).Value = updatedAt;
            }
            sheet.Cell(row, 10).Value = producto.CreatedBy;
            sheet.Cell(row, 11).Value = producto.UpdatedBy ?? string.Empty;
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

    private static void HeaderCell(IContainer container, string text)
    {
        container.Background(Colors.Grey.Lighten3).Padding(6).Text(text).Bold().FontSize(10);
    }

    private static void BodyCell(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Text(text).FontSize(9);
    }
}
