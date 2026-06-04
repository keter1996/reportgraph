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
                Content: RenderPage(graph, page)))
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

        if (graph.Semantics.BusinessGlossary.Terms.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Business Glossary");

            foreach (var term in graph.Semantics.BusinessGlossary.Terms
                         .OrderBy(term => term.DisplayName, StringComparer.OrdinalIgnoreCase)
                         .Take(20))
            {
                var aliases = term.Aliases.Count == 0 ? "N/A" : string.Join(", ", term.Aliases);
                builder.AppendLine($"- {term.DisplayName}: aliases={aliases}; mapped={term.MappedObjects.Count}");
            }
        }

        if (graph.Semantics.DocumentIndex.Documents.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Source Documents");

            foreach (var document in graph.Semantics.DocumentIndex.Documents
                         .OrderBy(document => document.Path, StringComparer.OrdinalIgnoreCase)
                         .Take(20))
            {
                builder.AppendLine($"- {document.Title} ({document.Path}): linkedObjects={document.LinkedObjects.Count}");
            }
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

        if (graph.Semantics.MeasureSemantics.Measures.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Measure Semantics");

            foreach (var measure in graph.Semantics.MeasureSemantics.Measures
                         .OrderBy(measure => measure.Table, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(measure => measure.Name, StringComparer.OrdinalIgnoreCase)
                         .Take(50))
            {
                builder.AppendLine($"- {measure.Table}.{measure.Name}: businessName={measure.BusinessName}; pattern={measure.FormulaPattern}; complexity={measure.Complexity}; core={measure.IsCoreMetric}");
            }
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

    private static string RenderPage(GraphModel graph, ReportGraphPageNode page)
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
            builder.AppendLine();
        }

        var intent = graph.Semantics.PageIntent.Pages.FirstOrDefault(item => string.Equals(item.PageId, page.PageId, StringComparison.OrdinalIgnoreCase));
        if (intent is not null)
        {
            builder.AppendLine("## Page Intent");
            builder.AppendLine($"- Topic: {intent.Topic ?? "N/A"}");
            builder.AppendLine($"- Primary question: {intent.PrimaryQuestion ?? "N/A"}");
            builder.AppendLine($"- Reading order: {string.Join(", ", intent.ReadingOrder)}");
            builder.AppendLine($"- Primary visuals: {string.Join(", ", intent.PrimaryVisualIds)}");
            builder.AppendLine();
        }

        var context = graph.Semantics.ContextSemantics.Pages.FirstOrDefault(item => string.Equals(item.PageId, page.PageId, StringComparison.OrdinalIgnoreCase));
        if (context is not null)
        {
            builder.AppendLine("## Semantic Context");
            builder.AppendLine($"- Common slicers: {string.Join(", ", context.CommonSlicers.Select(slicer => $"{slicer.Table}.{slicer.Name}"))}");
            builder.AppendLine($"- High impact dimensions: {string.Join(", ", context.HighImpactDimensions)}");
            builder.AppendLine($"- Default filters: {string.Join(", ", context.DefaultFilters.Select(filter => $"{filter.Table}.{filter.Field}={filter.Value ?? "any"}"))}");
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
