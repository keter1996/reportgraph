namespace ReportGraph.Core.Models;

public sealed record ReportGraphBuildInput(
    string Version,
    DateTimeOffset GeneratedAtUtc,
    ReportGraphSource Source,
    ReportInput Report,
    SemanticModelInput Model,
    IReadOnlyList<MarkdownDocumentInput>? Documents = null);

public sealed record MarkdownDocumentInput(
    string Path,
    string Content,
    DateTimeOffset LastModifiedUtc);

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
    IReadOnlyList<VisualFieldInput> Fields,
    IReadOnlyList<VisualFilterInput>? Filters = null);

public sealed record VisualFieldInput(
    string Role,
    string Table,
    string Field,
    FieldReferenceKind Kind);

public sealed record VisualFilterInput(
    string Table,
    string Field,
    IReadOnlyList<string> Values);

public sealed record SemanticModelInput(
    string? ModelName,
    IReadOnlyList<TableInput> Tables,
    IReadOnlyList<RelationshipInput> Relationships,
    IReadOnlyList<ColumnInput>? Columns = null,
    IReadOnlyList<MeasureInput>? Measures = null);

public sealed record TableInput(
    string Name,
    bool IsHidden,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> Measures);

public sealed record ColumnInput(
    string Table,
    string Name,
    string? DisplayFolder = null,
    string? FormatString = null);

public sealed record MeasureInput(
    string Table,
    string Name,
    string? DisplayFolder = null,
    string? FormatString = null,
    string? Expression = null);

public sealed record RelationshipInput(
    string RelationshipId,
    string FromTable,
    string FromColumn,
    string ToTable,
    string ToColumn,
    bool IsActive);
