namespace ReportGraph.Distribution.Models;

public sealed record McpToolDefinition(
    string Name,
    string Summary,
    string InputSchemaSummary,
    string OutputSummary);
