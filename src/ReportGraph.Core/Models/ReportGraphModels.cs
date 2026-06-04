namespace ReportGraph.Core.Models;

public sealed record ReportGraph(
    string Version,
    DateTimeOffset GeneratedAtUtc,
    ReportGraphSource Source,
    ReportGraphReportLayer Report,
    ReportGraphModelLayer Model,
    ReportGraphBindingLayer Bindings,
    ReportGraphSemanticLayer Semantics,
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

public sealed record ReportGraphSemanticLayer(
    ReportGraphBusinessGlossary BusinessGlossary,
    ReportGraphPageIntentLayer PageIntent,
    ReportGraphMeasureSemanticLayer MeasureSemantics,
    ReportGraphContextSemanticLayer ContextSemantics,
    ReportGraphDocumentIndex DocumentIndex)
{
    public static ReportGraphSemanticLayer Empty { get; } = new(
        BusinessGlossary: ReportGraphBusinessGlossary.Empty,
        PageIntent: ReportGraphPageIntentLayer.Empty,
        MeasureSemantics: ReportGraphMeasureSemanticLayer.Empty,
        ContextSemantics: ReportGraphContextSemanticLayer.Empty,
        DocumentIndex: ReportGraphDocumentIndex.Empty);
}

public sealed record ReportGraphBusinessGlossary(
    IReadOnlyList<ReportGraphBusinessTerm> Terms)
{
    public static ReportGraphBusinessGlossary Empty { get; } = new(Terms: []);
}

public sealed record ReportGraphBusinessTerm(
    string TermId,
    string DisplayName,
    IReadOnlyList<string> Aliases,
    string? Description,
    string? Unit,
    string? CanonicalName,
    IReadOnlyList<ReportGraphSemanticObjectReference> MappedObjects,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphPageIntentLayer(
    IReadOnlyList<ReportGraphPageIntentNode> Pages)
{
    public static ReportGraphPageIntentLayer Empty { get; } = new(Pages: []);
}

public sealed record ReportGraphPageIntentNode(
    string PageId,
    string? Topic,
    string? PrimaryQuestion,
    IReadOnlyList<string> ReadingOrder,
    IReadOnlyList<string> PrimaryVisualIds,
    IReadOnlyList<ReportGraphVisualRoleNode> VisualRoles,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphVisualRoleNode(
    string VisualId,
    VisualRole VisualRole,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphMeasureSemanticLayer(
    IReadOnlyList<ReportGraphMeasureSemanticNode> Measures)
{
    public static ReportGraphMeasureSemanticLayer Empty { get; } = new(Measures: []);
}

public sealed record ReportGraphMeasureSemanticNode(
    string Table,
    string Name,
    string? BusinessName,
    MeasureFormulaPattern FormulaPattern,
    string? BusinessTopic,
    IReadOnlyList<ReportGraphMeasureDependencyReference> DependsOnMeasures,
    IReadOnlyList<ReportGraphMeasureDependencyReference> DependsOnColumns,
    bool IsCoreMetric,
    SemanticComplexity Complexity,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphMeasureDependencyReference(
    string Table,
    string Name);

public sealed record ReportGraphContextSemanticLayer(
    IReadOnlyList<ReportGraphPageContextNode> Pages)
{
    public static ReportGraphContextSemanticLayer Empty { get; } = new(Pages: []);
}

public sealed record ReportGraphPageContextNode(
    string PageId,
    IReadOnlyList<ReportGraphFilterContextNode> DefaultFilters,
    IReadOnlyList<ReportGraphFilterContextNode> VisualFilters,
    IReadOnlyList<ReportGraphSemanticObjectReference> CommonSlicers,
    IReadOnlyList<string> HighImpactDimensions,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphFilterContextNode(
    string Scope,
    string? VisualId,
    string Table,
    string Field,
    string? Value = null);

public sealed record ReportGraphDocumentIndex(
    IReadOnlyList<ReportGraphDocumentNode> Documents)
{
    public static ReportGraphDocumentIndex Empty { get; } = new(Documents: []);
}

public sealed record ReportGraphDocumentNode(
    string DocumentId,
    string Path,
    string Title,
    string? Summary,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> TopicTags,
    IReadOnlyList<ReportGraphSemanticObjectReference> LinkedObjects,
    string? Scope,
    string? Version,
    Provenance Provenance = Provenance.Derived);

public sealed record ReportGraphSemanticObjectReference(
    SemanticObjectKind Kind,
    string Name,
    string? Table = null,
    string? PageId = null,
    string? VisualId = null);

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

public enum VisualRole
{
    Filter,
    Kpi,
    Trend,
    Comparison,
    Detail,
    Navigation,
    Annotation,
    Decoration,
    Unknown
}

public enum MeasureFormulaPattern
{
    Aggregate,
    Ratio,
    Rank,
    TimeIntelligence,
    RunningTotal,
    Variance,
    Classification,
    Custom,
    Unknown
}

public enum SemanticComplexity
{
    Low,
    Medium,
    High,
    Unknown
}

public enum SemanticObjectKind
{
    Report,
    Page,
    Visual,
    Table,
    Column,
    Measure,
    Relationship,
    Document,
    Command,
    Term,
    Unknown
}
