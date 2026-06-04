using ReportGraph.Core.Models;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.Query.Services;

public interface IReportGraphQueryService
{
    GraphModel GetGraph(GraphModel graph);
    ReportGraphPageNode? GetPage(GraphModel graph, string pageId);
    ReportGraphPageIntentNode? GetPageIntent(GraphModel graph, string pageId);
    ReportGraphPageContextNode? GetPageContext(GraphModel graph, string pageId);
    PageBindingsResult? GetPageBindings(GraphModel graph, string pageId);
    ReportGraphMeasureSemanticNode? GetMeasure(GraphModel graph, string measureName, string? tableName = null);
    MeasureLineageResult? GetMeasureLineage(GraphModel graph, string measureName, string? tableName = null);
    TermSearchResult SearchTerms(GraphModel graph, string query);
    ReportGraphDocumentNode? GetDocument(GraphModel graph, string documentIdOrPath);
    TableUsageResult? GetTableUsage(GraphModel graph, string tableName);
    ReportGraphVisualFieldBinding? GetVisual(GraphModel graph, string pageId, string visualId);
    ExploreResult Explore(GraphModel graph, ExploreQuery query);
}

public sealed class ReportGraphQueryService : IReportGraphQueryService
{
    public GraphModel GetGraph(GraphModel graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return graph;
    }

    public ReportGraphPageNode? GetPage(GraphModel graph, string pageId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);

        return graph.Report.Pages.FirstOrDefault(page => string.Equals(page.PageId, pageId, StringComparison.OrdinalIgnoreCase));
    }

    public ReportGraphPageIntentNode? GetPageIntent(GraphModel graph, string pageId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);

        return graph.Semantics.PageIntent.Pages.FirstOrDefault(page => string.Equals(page.PageId, pageId, StringComparison.OrdinalIgnoreCase));
    }

    public ReportGraphPageContextNode? GetPageContext(GraphModel graph, string pageId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);

        return graph.Semantics.ContextSemantics.Pages.FirstOrDefault(page => string.Equals(page.PageId, pageId, StringComparison.OrdinalIgnoreCase));
    }

    public PageBindingsResult? GetPageBindings(GraphModel graph, string pageId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);

        var page = GetPage(graph, pageId);
        if (page is null)
        {
            return null;
        }

        var tableBinding = graph.Bindings.PageToTables.FirstOrDefault(binding => string.Equals(binding.PageId, pageId, StringComparison.OrdinalIgnoreCase));
        var measureBinding = graph.Bindings.PageToMeasures.FirstOrDefault(binding => string.Equals(binding.PageId, pageId, StringComparison.OrdinalIgnoreCase));
        var visualBindings = graph.Bindings.VisualToFields
            .Where(binding => string.Equals(binding.PageId, pageId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(binding => binding.VisualId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PageBindingsResult(
            Page: page,
            Tables: tableBinding?.Tables ?? [],
            Measures: measureBinding?.Measures ?? [],
            Visuals: visualBindings);
    }

    public TableUsageResult? GetTableUsage(GraphModel graph, string tableName)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        var table = graph.Model.Tables.FirstOrDefault(item => string.Equals(item.Name, tableName, StringComparison.OrdinalIgnoreCase));
        if (table is null)
        {
            return null;
        }

        var visuals = graph.Bindings.VisualToFields
            .Where(binding => binding.Fields.Any(field => string.Equals(field.Table, tableName, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(binding => binding.PageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(binding => binding.VisualId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new TableUsageResult(table, visuals);
    }

    public ReportGraphMeasureSemanticNode? GetMeasure(GraphModel graph, string measureName, string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(measureName);

        var matches = graph.Semantics.MeasureSemantics.Measures
            .Where(measure => string.Equals(measure.Name, measureName, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            matches = matches.Where(measure => string.Equals(measure.Table, tableName, StringComparison.OrdinalIgnoreCase));
        }

        return matches
            .OrderBy(measure => measure.Table, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public MeasureLineageResult? GetMeasureLineage(GraphModel graph, string measureName, string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(measureName);

        var root = GetMeasure(graph, measureName, tableName);
        if (root is null)
        {
            return null;
        }

        var measureByIdentity = graph.Semantics.MeasureSemantics.Measures
            .GroupBy(measure => CreateIdentity(measure.Table, measure.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var measures = new List<ReportGraphMeasureSemanticNode>();
        var measureEdges = new HashSet<MeasureLineageMeasureEdge>();
        var columnEdges = new HashSet<MeasureLineageColumnEdge>();

        VisitMeasure(root);

        return new MeasureLineageResult(
            Root: new ReportGraphMeasureDependencyReference(root.Table, root.Name),
            Measures: measures
                .OrderBy(measure => measure.Table, StringComparer.OrdinalIgnoreCase)
                .ThenBy(measure => measure.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            MeasureEdges: measureEdges
                .OrderBy(edge => edge.FromTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.FromMeasure, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ToTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ToMeasure, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ColumnEdges: columnEdges
                .OrderBy(edge => edge.FromTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.FromMeasure, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ToTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ToColumn, StringComparer.OrdinalIgnoreCase)
                .ToArray());

        void VisitMeasure(ReportGraphMeasureSemanticNode measure)
        {
            var identity = CreateIdentity(measure.Table, measure.Name);
            if (!visited.Add(identity))
            {
                return;
            }

            measures.Add(measure);

            foreach (var columnDependency in measure.DependsOnColumns)
            {
                columnEdges.Add(new MeasureLineageColumnEdge(
                    FromTable: measure.Table,
                    FromMeasure: measure.Name,
                    ToTable: columnDependency.Table,
                    ToColumn: columnDependency.Name));
            }

            foreach (var measureDependency in measure.DependsOnMeasures)
            {
                measureEdges.Add(new MeasureLineageMeasureEdge(
                    FromTable: measure.Table,
                    FromMeasure: measure.Name,
                    ToTable: measureDependency.Table,
                    ToMeasure: measureDependency.Name));

                if (measureByIdentity.TryGetValue(CreateIdentity(measureDependency.Table, measureDependency.Name), out var nextMeasure))
                {
                    VisitMeasure(nextMeasure);
                }
            }
        }
    }

    public TermSearchResult SearchTerms(GraphModel graph, string query)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = query.Trim();
        var matches = graph.Semantics.BusinessGlossary.Terms
            .Select(term => new
            {
                Term = term,
                Score = ScoreTerm(term, normalizedQuery)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Term.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new TermSearchMatch(
                Term: item.Term,
                Score: item.Score,
                MatchedBy: ResolveTermMatchedBy(item.Term, normalizedQuery)))
            .ToArray();

        return new TermSearchResult(normalizedQuery, matches);
    }

    public ReportGraphDocumentNode? GetDocument(GraphModel graph, string documentIdOrPath)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentIdOrPath);

        var normalized = documentIdOrPath.Trim().Replace('\\', '/');
        return graph.Semantics.DocumentIndex.Documents
            .FirstOrDefault(document =>
                string.Equals(document.DocumentId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(document.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public ReportGraphVisualFieldBinding? GetVisual(GraphModel graph, string pageId, string visualId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(visualId);

        return graph.Bindings.VisualToFields.FirstOrDefault(binding =>
            string.Equals(binding.PageId, pageId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(binding.VisualId, visualId, StringComparison.OrdinalIgnoreCase));
    }

    public ExploreResult Explore(GraphModel graph, ExploreQuery query)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(query);

        return query.Mode switch
        {
            ExploreMode.FromPage => ExploreFromPage(graph, query.Identifier),
            ExploreMode.FromTable => ExploreFromTable(graph, query.Identifier),
            _ => new ExploreResult(query.Mode, query.Identifier, [])
        };
    }

    private static ExploreResult ExploreFromPage(GraphModel graph, string identifier)
    {
        var bindings = graph.Bindings.PageToTables.FirstOrDefault(binding => string.Equals(binding.PageId, identifier, StringComparison.OrdinalIgnoreCase));
        var items = bindings?.Tables.Select(table => $"table:{table}").ToArray() ?? [];
        return new ExploreResult(ExploreMode.FromPage, identifier, items);
    }

    private static ExploreResult ExploreFromTable(GraphModel graph, string identifier)
    {
        var table = graph.Model.Tables.FirstOrDefault(item => string.Equals(item.Name, identifier, StringComparison.OrdinalIgnoreCase));
        var items = table?.UsedByPages.Select(page => $"page:{page}").ToArray() ?? [];
        return new ExploreResult(ExploreMode.FromTable, identifier, items);
    }

    private static string CreateIdentity(string table, string name)
    {
        return $"{table}::{name}";
    }

    private static int ScoreTerm(ReportGraphBusinessTerm term, string query)
    {
        if (term.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (term.CanonicalName?.Equals(query, StringComparison.OrdinalIgnoreCase) == true)
        {
            return 95;
        }

        if (term.Aliases.Any(alias => alias.Equals(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 90;
        }

        if (term.MappedObjects.Any(reference => reference.Name.Equals(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 85;
        }

        if (term.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            term.Aliases.Any(alias => alias.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
            term.MappedObjects.Any(reference => reference.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 60;
        }

        return 0;
    }

    private static string ResolveTermMatchedBy(ReportGraphBusinessTerm term, string query)
    {
        if (term.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return "displayName";
        }

        if (term.CanonicalName?.Equals(query, StringComparison.OrdinalIgnoreCase) == true)
        {
            return "canonicalName";
        }

        if (term.Aliases.Any(alias => alias.Equals(query, StringComparison.OrdinalIgnoreCase)))
        {
            return "alias";
        }

        if (term.MappedObjects.Any(reference => reference.Name.Equals(query, StringComparison.OrdinalIgnoreCase)))
        {
            return "mappedObject";
        }

        return "partial";
    }
}

public sealed record PageBindingsResult(
    ReportGraphPageNode Page,
    IReadOnlyList<string> Tables,
    IReadOnlyList<string> Measures,
    IReadOnlyList<ReportGraphVisualFieldBinding> Visuals);

public sealed record TableUsageResult(
    ReportGraphTableNode Table,
    IReadOnlyList<ReportGraphVisualFieldBinding> Visuals);

public sealed record MeasureLineageResult(
    ReportGraphMeasureDependencyReference Root,
    IReadOnlyList<ReportGraphMeasureSemanticNode> Measures,
    IReadOnlyList<MeasureLineageMeasureEdge> MeasureEdges,
    IReadOnlyList<MeasureLineageColumnEdge> ColumnEdges);

public sealed record MeasureLineageMeasureEdge(
    string FromTable,
    string FromMeasure,
    string ToTable,
    string ToMeasure);

public sealed record MeasureLineageColumnEdge(
    string FromTable,
    string FromMeasure,
    string ToTable,
    string ToColumn);

public sealed record TermSearchResult(
    string Query,
    IReadOnlyList<TermSearchMatch> Matches);

public sealed record TermSearchMatch(
    ReportGraphBusinessTerm Term,
    int Score,
    string MatchedBy);

public sealed record ExploreQuery(
    ExploreMode Mode,
    string Identifier);

public sealed record ExploreResult(
    ExploreMode Mode,
    string Identifier,
    IReadOnlyList<string> Items);

public enum ExploreMode
{
    FromPage,
    FromTable
}
