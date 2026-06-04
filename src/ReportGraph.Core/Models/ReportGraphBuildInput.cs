namespace ReportGraph.Core.Models;

public sealed record ReportGraphBuildInput(
    string Version,
    DateTimeOffset GeneratedAtUtc,
    ReportGraphSource Source,
    ReportInput Report,
    SemanticModelInput Model);

public sealed record ReportInput(
    string? ReportName,
    string? ActivePageId,
    DateTimeOffset PagesLastModifiedUtc,
    IReadOnlyList<ReportPageInput> Pages);

public sealed record ReportPageInput(
    string PageId,
    string DisplayName,
    int Ordinal,
    IReadOnlyList<VisualInput> Visuals);

public sealed record VisualInput(
    string VisualId,
    string VisualType,
    IReadOnlyList<VisualFieldInput> Fields);

public sealed record VisualFieldInput(
    string Role,
    string Table,
    string Field,
    FieldReferenceKind Kind);

public sealed record SemanticModelInput(
    string? ModelName,
    IReadOnlyList<TableInput> Tables,
    IReadOnlyList<RelationshipInput> Relationships);

public sealed record TableInput(
    string Name,
    bool IsHidden,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> Measures);

public sealed record RelationshipInput(
    string RelationshipId,
    string FromTable,
    string FromColumn,
    string ToTable,
    string ToColumn,
    bool IsActive);
