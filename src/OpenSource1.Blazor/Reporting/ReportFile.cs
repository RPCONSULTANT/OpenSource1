namespace OpenSource1.Blazor.Reporting;

public sealed record ReportFile(string FileName, string ContentType, byte[] Content);
