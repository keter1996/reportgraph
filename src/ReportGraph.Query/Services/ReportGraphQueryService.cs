using ReportGraph.Core.Models;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.Query.Services;

public interface IReportGraphQueryService
{
    GraphModel GetGraph(GraphModel graph);
    ReportGraphPageNode? GetPage(GraphModel graph, string pageId);
    PageBindingsResult? GetPageBindings(GraphModel graph, string pageId);
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
}

public sealed record PageBindingsResult(
    ReportGraphPageNode Page,
    IReadOnlyList<string> Tables,
    IReadOnlyList<string> Measures,
    IReadOnlyList<ReportGraphVisualFieldBinding> Visuals);

public sealed record TableUsageResult(
    ReportGraphTableNode Table,
    IReadOnlyList<ReportGraphVisualFieldBinding> Visuals);

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
