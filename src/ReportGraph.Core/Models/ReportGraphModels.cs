namespace ReportGraph.Core.Models;

public sealed record ReportGraph(
    string Version,
    DateTimeOffset GeneratedAtUtc,
    ReportGraphSource Source,
    ReportGraphReportLayer Report,
    ReportGraphModelLayer Model,
    ReportGraphBindingLayer Bindings,
    ReportGraphDiagnostics Diagnostics);

public sealed record ReportGraphSource(
    string InstanceId,
    string PbipProjectPath,
    string ReportRootPath,
    string? ModelName);

public sealed record ReportGraphReportLayer(
    string? ReportName,
    string? ActivePageId,
    IReadOnlyList<ReportGraphPageNode> Pages,
    IReadOnlyList<ReportGraphStoryStep> Storyline);

public sealed record ReportGraphPageNode(
    string PageId,
    string DisplayName,
    int Ordinal,
    StoryRole StoryRole,
    IReadOnlyList<string> DominantVisualTypes,
    IReadOnlyList<string> FocusTables,
    IReadOnlyList<string> FocusMeasures,
    string? BusinessQuestion,
    string? NarrativeSummary,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphStoryStep(
    string StepId,
    string PageId,
    int Ordinal,
    StoryRole StoryRole,
    string Summary,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphModelLayer(
    ReportGraphModelOverview Summary,
    IReadOnlyList<ReportGraphTableNode> Tables,
    IReadOnlyList<ReportGraphRelationshipEdge> Relationships);

public sealed record ReportGraphModelOverview(
    string? ModelName,
    int TableCount,
    int ColumnCount,
    int MeasureCount,
    int RelationshipCount);

public sealed record ReportGraphTableNode(
    string Name,
    SemanticRole SemanticRole,
    bool IsHidden,
    int ColumnCount,
    int MeasureCount,
    int RelationshipDegree,
    int UsedByVisualCount,
    IReadOnlyList<string> UsedByPages,
    IReadOnlyList<string> Measures,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphRelationshipEdge(
    string RelationshipId,
    string FromTable,
    string FromColumn,
    string ToTable,
    string ToColumn,
    bool IsActive,
    Provenance Provenance = Provenance.Declared);

public sealed record ReportGraphBindingLayer(
    IReadOnlyList<ReportGraphPageTableBinding> PageToTables,
    IReadOnlyList<ReportGraphPageMeasureBinding> PageToMeasures,
    IReadOnlyList<ReportGraphVisualFieldBinding> VisualToFields);

public sealed record ReportGraphPageTableBinding(
    string PageId,
    IReadOnlyList<string> Tables,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphPageMeasureBinding(
    string PageId,
    IReadOnlyList<string> Measures,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphVisualFieldBinding(
    string PageId,
    string VisualId,
    string VisualType,
    IReadOnlyList<ReportGraphFieldReference> Fields,
    Provenance Provenance = Provenance.Declared);

public sealed record ReportGraphFieldReference(
    string Role,
    string Table,
    string Field,
    FieldReferenceKind Kind);

public sealed record ReportGraphDiagnostics(
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Notes);

public enum StoryRole
{
    Overview,
    Trend,
    Breakdown,
    Composition,
    Detail,
    Filtering,
    Action,
    Unknown
}

public enum SemanticRole
{
    Fact,
    Dimension,
    Bridge,
    Lookup,
    Unknown
}

public enum Provenance
{
    Declared,
    Derived,
    Heuristic
}

public enum FieldReferenceKind
{
    Column,
    Measure
}
