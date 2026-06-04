using System.Text;
using ReportGraph.Core.Models;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.Core.Services;

public interface IReportGraphContextRenderer
{
    RenderedReportGraphContext Render(GraphModel graph);
}

public sealed class ReportGraphContextRenderer : IReportGraphContextRenderer
{
    public RenderedReportGraphContext Render(GraphModel graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var pages = graph.Report.Pages
            .OrderBy(page => page.Ordinal)
            .Select(page => new RenderedMarkdownDocument(
                RelativePath: Path.Combine("pages", $"{page.PageId}.md").Replace('\\', '/'),
                Content: RenderPage(page)))
            .ToArray();

        return new RenderedReportGraphContext(
            ReportSummary: new RenderedMarkdownDocument("report-summary.md", RenderReportSummary(graph)),
            ModelSummary: new RenderedMarkdownDocument("model.md", RenderModelSummary(graph)),
            BindingsSummary: new RenderedMarkdownDocument("bindings.md", RenderBindingsSummary(graph)),
            PageSummaries: pages);
    }

    private static string RenderReportSummary(GraphModel graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {graph.Report.ReportName ?? "Report Graph"}");
        builder.AppendLine();
        builder.AppendLine($"- Active page: {graph.Report.ActivePageId ?? "N/A"}");
        builder.AppendLine($"- Page count: {graph.Report.Pages.Count}");
        builder.AppendLine($"- Model: {graph.Model.Summary.ModelName ?? "Unknown"}");
        builder.AppendLine();
        builder.AppendLine("## Storyline");

        foreach (var page in graph.Report.Pages.OrderBy(page => page.Ordinal))
        {
            builder.AppendLine($"- {page.Ordinal + 1}. {page.DisplayName} [{page.StoryRole}]");
        }

        builder.AppendLine();
        builder.AppendLine("## Primary Themes");

        foreach (var table in graph.Model.Tables
                     .OrderByDescending(table => table.UsedByVisualCount)
                     .ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
                     .Take(5))
        {
            builder.AppendLine($"- {table.Name}: pages={string.Join(", ", table.UsedByPages)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderModelSummary(GraphModel graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Model: {graph.Model.Summary.ModelName ?? "Unknown"}");
        builder.AppendLine();
        builder.AppendLine($"- Tables: {graph.Model.Summary.TableCount}");
        builder.AppendLine($"- Columns: {graph.Model.Summary.ColumnCount}");
        builder.AppendLine($"- Measures: {graph.Model.Summary.MeasureCount}");
        builder.AppendLine($"- Relationships: {graph.Model.Summary.RelationshipCount}");
        builder.AppendLine();
        builder.AppendLine("## Tables");

        foreach (var table in graph.Model.Tables.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {table.Name} [{table.SemanticRole}]");
            builder.AppendLine($"  Used by pages: {string.Join(", ", table.UsedByPages)}");
            builder.AppendLine($"  Measures: {string.Join(", ", table.Measures)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderBindingsSummary(GraphModel graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Bindings");
        builder.AppendLine();
        builder.AppendLine("## Page to Tables");

        foreach (var binding in graph.Bindings.PageToTables.OrderBy(binding => binding.PageId, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {binding.PageId}: {string.Join(", ", binding.Tables)}");
        }

        builder.AppendLine();
        builder.AppendLine("## Page to Measures");

        foreach (var binding in graph.Bindings.PageToMeasures.OrderBy(binding => binding.PageId, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {binding.PageId}: {string.Join(", ", binding.Measures)}");
        }

        builder.AppendLine();
        builder.AppendLine("## Visual to Fields");

        foreach (var binding in graph.Bindings.VisualToFields
                     .OrderBy(binding => binding.PageId, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(binding => binding.VisualId, StringComparer.OrdinalIgnoreCase))
        {
            var fields = string.Join(", ", binding.Fields.Select(field => $"{field.Role}={field.Table}.{field.Field}"));
            builder.AppendLine($"- {binding.PageId}/{binding.VisualId} ({binding.VisualType}): {fields}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderPage(ReportGraphPageNode page)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {page.DisplayName}");
        builder.AppendLine();
        builder.AppendLine($"- Page ID: {page.PageId}");
        builder.AppendLine($"- Ordinal: {page.Ordinal}");
        builder.AppendLine($"- Story role: {page.StoryRole}");
        builder.AppendLine($"- Dominant visuals: {string.Join(", ", page.DominantVisualTypes)}");
        builder.AppendLine($"- Focus tables: {string.Join(", ", page.FocusTables)}");
        builder.AppendLine($"- Focus measures: {string.Join(", ", page.FocusMeasures)}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(page.BusinessQuestion))
        {
            builder.AppendLine("## Business Question");
            builder.AppendLine(page.BusinessQuestion);
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(page.NarrativeSummary))
        {
            builder.AppendLine("## Narrative Summary");
            builder.AppendLine(page.NarrativeSummary);
        }

        return builder.ToString().TrimEnd();
    }
}

public sealed record RenderedReportGraphContext(
    RenderedMarkdownDocument ReportSummary,
    RenderedMarkdownDocument ModelSummary,
    RenderedMarkdownDocument BindingsSummary,
    IReadOnlyList<RenderedMarkdownDocument> PageSummaries);

public sealed record RenderedMarkdownDocument(
    string RelativePath,
    string Content);
