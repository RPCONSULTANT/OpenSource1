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

    private static void HeaderCell(IContainer container, string text)
    {
        container.Background(Colors.Grey.Lighten3).Padding(6).Text(text).Bold().FontSize(10);
    }

    private static void BodyCell(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Text(text).FontSize(9);
    }
}
