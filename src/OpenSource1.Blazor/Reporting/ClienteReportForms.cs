namespace OpenSource1.Blazor.Reporting;

public sealed class ClienteSelectionReportForm
{
    public List<Guid> SelectedIds { get; set; } = [];
}

public sealed class ClienteHistoricalReportForm
{
    public string? Status { get; set; }
}

public sealed class ProductoSelectionReportForm
{
    public List<Guid> SelectedIds { get; set; } = [];
    public string? StockState { get; set; }
}

public sealed class ProductoHistoricalReportForm
{
    public string? StockState { get; set; }
}
