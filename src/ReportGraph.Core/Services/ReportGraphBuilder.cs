using ReportGraph.Core.Models;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.Core.Services;

public interface IReportGraphBuilder
{
    GraphModel Build(ReportGraphBuildInput input);
}

public sealed class ReportGraphBuilder : IReportGraphBuilder
{
    private readonly IReportGraphResolver resolver;
    private readonly IReportGraphSemanticBuilder semanticBuilder;

    public ReportGraphBuilder()
        : this(new ReportGraphResolver(), new ReportGraphSemanticBuilder())
    {
    }

    public ReportGraphBuilder(IReportGraphResolver resolver, IReportGraphSemanticBuilder semanticBuilder)
    {
        this.resolver = resolver;
        this.semanticBuilder = semanticBuilder;
    }

    public GraphModel Build(ReportGraphBuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var visualBindings = BuildVisualBindings(input.Report.Pages);
        var pageToTables = BuildPageTableBindings(input.Report.Pages);
        var pageToMeasures = BuildPageMeasureBindings(input.Report.Pages);
        var pages = BuildPageNodes(input.Report.Pages, pageToTables, pageToMeasures, resolver);
        var storyline = BuildStoryline(pages);
        var modelTables = BuildModelTables(input.Model.Tables, input.Model.Relationships, input.Report.Pages, resolver);
        var modelRelationships = BuildRelationships(input.Model.Relationships);
        var semantics = semanticBuilder.Build(input, pages);

        return new GraphModel(
            Version: input.Version,
            GeneratedAtUtc: input.GeneratedAtUtc,
            Source: input.Source,
            Report: new ReportGraphReportLayer(
                ReportName: input.Report.ReportName,
                ActivePageId: input.Report.ActivePageId,
                Pages: pages,
                Storyline: storyline),
            Model: new ReportGraphModelLayer(
                Summary: new ReportGraphModelOverview(
                    ModelName: input.Model.ModelName,
                    TableCount: input.Model.Tables.Count,
                    ColumnCount: input.Model.Tables.Sum(table => table.Columns.Count),
                    MeasureCount: input.Model.Tables.Sum(table => table.Measures.Count),
                    RelationshipCount: input.Model.Relationships.Count),
                Tables: modelTables,
                Relationships: modelRelationships),
            Bindings: new ReportGraphBindingLayer(
                PageToTables: pageToTables,
                PageToMeasures: pageToMeasures,
                VisualToFields: visualBindings),
            Semantics: semantics,
            Diagnostics: new ReportGraphDiagnostics(
                Warnings: [],
                Notes: ["Graph built from adapter-neutral input contract."]));
    }

    private static IReadOnlyList<ReportGraphPageNode> BuildPageNodes(
        IReadOnlyList<ReportPageInput> pages,
        IReadOnlyList<ReportGraphPageTableBinding> pageToTables,
        IReadOnlyList<ReportGraphPageMeasureBinding> pageToMeasures,
        IReportGraphResolver resolver)
    {
        return pages
            .OrderBy(page => page.Ordinal)
            .Select(page =>
            {
                var resolved = resolver.ResolvePage(page, pageToTables, pageToMeasures);

                return new ReportGraphPageNode(
                    PageId: page.PageId,
                    DisplayName: page.DisplayName,
                    Ordinal: page.Ordinal,
                    StoryRole: resolved.StoryRole,
                    DominantVisualTypes: page.Visuals
                        .GroupBy(visual => visual.VisualType, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(group => group.Count())
                        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.Key)
                        .ToArray(),
                    FocusTables: resolved.FocusTables,
                    FocusMeasures: resolved.FocusMeasures,
                    BusinessQuestion: resolved.BusinessQuestion,
                    NarrativeSummary: resolved.NarrativeSummary,
                    Provenance: Provenance.Derived);
            })
            .ToArray();
    }

    private static IReadOnlyList<ReportGraphStoryStep> BuildStoryline(IReadOnlyList<ReportGraphPageNode> pages)
    {
        return pages
            .OrderBy(page => page.Ordinal)
            .Select(page => new ReportGraphStoryStep(
                StepId: $"story-{page.PageId}",
                PageId: page.PageId,
                Ordinal: page.Ordinal,
                StoryRole: page.StoryRole,
                Summary: page.DisplayName,
                Provenance: Provenance.Derived))
            .ToArray();
    }

    private static IReadOnlyList<ReportGraphVisualFieldBinding> BuildVisualBindings(IReadOnlyList<ReportPageInput> pages)
    {
        return pages
            .OrderBy(page => page.Ordinal)
            .SelectMany(page => page.Visuals
                .OrderBy(visual => visual.VisualId, StringComparer.Ordinal)
                .Select(visual => new ReportGraphVisualFieldBinding(
                    PageId: page.PageId,
                    VisualId: visual.VisualId,
                    VisualType: visual.VisualType,
                    Fields: visual.Fields
                        .OrderBy(field => field.Role, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(field => field.Table, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(field => field.Field, StringComparer.OrdinalIgnoreCase)
                        .Select(field => new ReportGraphFieldReference(
                            Role: field.Role,
                            Table: field.Table,
                            Field: field.Field,
                            Kind: field.Kind))
                        .ToArray(),
                    Provenance: Provenance.Declared)))
            .ToArray();
    }

    private static IReadOnlyList<ReportGraphPageTableBinding> BuildPageTableBindings(IReadOnlyList<ReportPageInput> pages)
    {
        return pages
            .OrderBy(page => page.Ordinal)
            .Select(page => new ReportGraphPageTableBinding(
                PageId: page.PageId,
                Tables: page.Visuals
                    .SelectMany(visual => visual.Fields)
                    .Select(field => field.Table)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Provenance: Provenance.Derived))
            .ToArray();
    }

    private static IReadOnlyList<ReportGraphPageMeasureBinding> BuildPageMeasureBindings(IReadOnlyList<ReportPageInput> pages)
    {
        return pages
            .OrderBy(page => page.Ordinal)
            .Select(page => new ReportGraphPageMeasureBinding(
                PageId: page.PageId,
                Measures: page.Visuals
                    .SelectMany(visual => visual.Fields)
                    .Where(field => field.Kind == FieldReferenceKind.Measure)
                    .Select(field => field.Field)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Provenance: Provenance.Derived))
            .ToArray();
    }

    private static IReadOnlyList<ReportGraphTableNode> BuildModelTables(
        IReadOnlyList<TableInput> tables,
        IReadOnlyList<RelationshipInput> relationships,
        IReadOnlyList<ReportPageInput> pages,
        IReportGraphResolver resolver)
    {
        var visualUsages = pages
            .SelectMany(page => page.Visuals.Select(visual => new
            {
                page.PageId,
                Tables = visual.Fields.Select(field => field.Table).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            }))
            .ToArray();

        return tables
            .OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
            .Select(table =>
            {
                var usedByPages = visualUsages
                    .Where(usage => usage.Tables.Contains(table.Name, StringComparer.OrdinalIgnoreCase))
                    .Select(usage => usage.PageId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var usedByVisualCount = visualUsages.Count(usage => usage.Tables.Contains(table.Name, StringComparer.OrdinalIgnoreCase));

                return new ReportGraphTableNode(
                    Name: table.Name,
                    SemanticRole: resolver.ResolveTableRole(table, relationships),
                    IsHidden: table.IsHidden,
                    ColumnCount: table.Columns.Count,
                    MeasureCount: table.Measures.Count,
                    RelationshipDegree: relationships.Count(relationship =>
                        string.Equals(relationship.FromTable, table.Name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(relationship.ToTable, table.Name, StringComparison.OrdinalIgnoreCase)),
                    UsedByVisualCount: usedByVisualCount,
                    UsedByPages: usedByPages,
                    Measures: table.Measures.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                    Provenance: Provenance.Derived);
            })
            .ToArray();
    }

    private static IReadOnlyList<ReportGraphRelationshipEdge> BuildRelationships(IReadOnlyList<RelationshipInput> relationships)
    {
        return relationships
            .OrderBy(relationship => relationship.RelationshipId, StringComparer.OrdinalIgnoreCase)
            .Select(relationship => new ReportGraphRelationshipEdge(
                RelationshipId: relationship.RelationshipId,
                FromTable: relationship.FromTable,
                FromColumn: relationship.FromColumn,
                ToTable: relationship.ToTable,
                ToColumn: relationship.ToColumn,
                IsActive: relationship.IsActive,
                Provenance: Provenance.Declared))
            .ToArray();
    }
}
