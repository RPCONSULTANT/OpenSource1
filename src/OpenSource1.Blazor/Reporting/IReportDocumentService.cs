using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Blazor.Reporting;

public interface IReportDocumentService
{
    ReportFile GenerateClientesReport(IReadOnlyList<SocioNegocioResponse> clientes, string title);
    ReportFile GenerateProductosReport(IReadOnlyList<ProductoResponse> productos, string title);
    ReportFile GenerateClientesExcel(IReadOnlyList<SocioNegocioResponse> clientes, string title);
    ReportFile GenerateProductosExcel(IReadOnlyList<ProductoResponse> productos, string title);
    ReportFile GenerateClientesRawExcel(IReadOnlyList<SocioNegocioResponse> clientes);
    ReportFile GenerateProductosRawExcel(IReadOnlyList<ProductoResponse> productos);
}
