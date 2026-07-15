namespace OpenSource1.Blazor.Components;

public sealed record ChartDataset(
    string Label,
    IReadOnlyList<double> Data,
    IReadOnlyList<string>? BackgroundColor = null,
    string? BorderColor = null,
    bool Fill = false,
    double Tension = 0.35);

public static class ChartPalette
{
    public static readonly string[] Colors =
    [
        "#2155d9", "#22c55e", "#f59e0b", "#ef4444", "#8b5cf6",
        "#06b6d4", "#ec4899", "#84cc16", "#f97316", "#64748b"
    ];

    public static string Pick(int index) => Colors[index % Colors.Length];
}
