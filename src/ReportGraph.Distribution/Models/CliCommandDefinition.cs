namespace ReportGraph.Distribution.Models;

public sealed record CliCommandDefinition(
    string Name,
    string Summary,
    string Usage,
    IReadOnlyList<string> Aliases);
