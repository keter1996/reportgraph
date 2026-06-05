namespace ReportGraph.Core.Models;

public sealed record ReportGraphDirtyState(
    string Reason,
    DateTimeOffset MarkedAtUtc);
