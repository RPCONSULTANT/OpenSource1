using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Blazor.Reporting;

public interface IReportDocumentService
{
    ReportFile GenerateClientesReport(IReadOnlyList<ClienteResponse> clientes, string title);
    ReportFile GenerateProductosReport(IReadOnlyList<ProductoResponse> productos, string title);
}
