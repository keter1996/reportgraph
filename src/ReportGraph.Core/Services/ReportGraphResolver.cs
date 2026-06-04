using ReportGraph.Core.Models;

namespace ReportGraph.Core.Services;

public interface IReportGraphResolver
{
    ResolvedPageData ResolvePage(
        ReportPageInput page,
        IReadOnlyList<ReportGraphPageTableBinding> pageToTables,
        IReadOnlyList<ReportGraphPageMeasureBinding> pageToMeasures);

    SemanticRole ResolveTableRole(TableInput table, IReadOnlyList<RelationshipInput> relationships);
}

public sealed class ReportGraphResolver : IReportGraphResolver
{
    public ResolvedPageData ResolvePage(
        ReportPageInput page,
        IReadOnlyList<ReportGraphPageTableBinding> pageToTables,
        IReadOnlyList<ReportGraphPageMeasureBinding> pageToMeasures)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pageToTables);
        ArgumentNullException.ThrowIfNull(pageToMeasures);

        var visualTypeCounts = page.Visuals
            .GroupBy(visual => visual.VisualType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var storyRole = ResolveStoryRole(page.DisplayName, visualTypeCounts);
        var focusTables = ResolveFocusTables(page);
        var focusMeasures = ResolveFocusMeasures(page);
        var businessQuestion = ResolveBusinessQuestion(page.DisplayName, storyRole, focusTables, focusMeasures);
        var narrativeSummary = ResolveNarrativeSummary(page.DisplayName, storyRole, focusTables, focusMeasures);

        return new ResolvedPageData(
            StoryRole: storyRole,
            FocusTables: focusTables,
            FocusMeasures: focusMeasures,
            BusinessQuestion: businessQuestion,
            NarrativeSummary: narrativeSummary);
    }

    public SemanticRole ResolveTableRole(TableInput table, IReadOnlyList<RelationshipInput> relationships)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(relationships);

        if (table.Name.StartsWith("Fact", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticRole.Fact;
        }

        if (table.Name.StartsWith("Dim", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticRole.Dimension;
        }

        var relationshipDegree = relationships.Count(relationship =>
            string.Equals(relationship.FromTable, table.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relationship.ToTable, table.Name, StringComparison.OrdinalIgnoreCase));

        if (table.Measures.Count > 0 && relationshipDegree > 1)
        {
            return SemanticRole.Fact;
        }

        if (table.Columns.Count >= 3 && table.Measures.Count == 0)
        {
            return SemanticRole.Lookup;
        }

        return SemanticRole.Unknown;
    }

    private static StoryRole ResolveStoryRole(string displayName, IReadOnlyDictionary<string, int> visualTypeCounts)
    {
        var normalizedName = displayName.Trim();
        var hasCard = HasVisual(visualTypeCounts, "card");
        var hasLine = HasVisual(visualTypeCounts, "linechart");
        var hasTable = HasVisual(visualTypeCounts, "table") || HasVisual(visualTypeCounts, "matrix");
        var hasBar = HasVisual(visualTypeCounts, "bar") || HasVisual(visualTypeCounts, "column") || HasVisual(visualTypeCounts, "clusteredcolumnchart");

        if (normalizedName.Contains("总览", StringComparison.OrdinalIgnoreCase) || normalizedName.Contains("overview", StringComparison.OrdinalIgnoreCase))
        {
            return StoryRole.Overview;
        }

        if (normalizedName.Contains("趋势", StringComparison.OrdinalIgnoreCase) || normalizedName.Contains("trend", StringComparison.OrdinalIgnoreCase))
        {
            return StoryRole.Trend;
        }

        if (normalizedName.Contains("明细", StringComparison.OrdinalIgnoreCase) || normalizedName.Contains("detail", StringComparison.OrdinalIgnoreCase))
        {
            return StoryRole.Detail;
        }

        if (hasCard && hasLine)
        {
            return StoryRole.Overview;
        }

        if (hasLine)
        {
            return StoryRole.Trend;
        }

        if (hasTable)
        {
            return StoryRole.Detail;
        }

        if (hasBar)
        {
            return StoryRole.Breakdown;
        }

        return StoryRole.Unknown;
    }

    private static IReadOnlyList<string> ResolveFocusTables(ReportPageInput page)
    {
        return page.Visuals
            .SelectMany(visual => visual.Fields)
            .GroupBy(field => field.Table, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveFocusMeasures(ReportPageInput page)
    {
        return page.Visuals
            .SelectMany(visual => visual.Fields
                .Where(field => field.Kind == FieldReferenceKind.Measure)
                .Select(field => new
                {
                    field.Field,
                    Weight = GetVisualWeight(visual.VisualType)
                }))
            .GroupBy(item => item.Field, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(item => item.Weight))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .ToArray();
    }

    private static string ResolveBusinessQuestion(
        string displayName,
        StoryRole storyRole,
        IReadOnlyList<string> focusTables,
        IReadOnlyList<string> focusMeasures)
    {
        var primaryMeasure = focusMeasures.FirstOrDefault() ?? "关键指标";
        var primaryTable = focusTables.FirstOrDefault() ?? "核心主题";

        return storyRole switch
        {
            StoryRole.Overview => $"{displayName} 主要用于回答当前 {primaryMeasure} 的整体表现如何。",
            StoryRole.Trend => $"{displayName} 主要用于回答 {primaryMeasure} 随时间如何变化。",
            StoryRole.Detail => $"{displayName} 主要用于回答 {primaryTable} 的明细情况如何。",
            StoryRole.Breakdown => $"{displayName} 主要用于回答 {primaryMeasure} 由哪些维度构成。",
            _ => $"{displayName} 主要围绕 {primaryTable} 和 {primaryMeasure} 展开。"
        };
    }

    private static string ResolveNarrativeSummary(
        string displayName,
        StoryRole storyRole,
        IReadOnlyList<string> focusTables,
        IReadOnlyList<string> focusMeasures)
    {
        var tablesText = focusTables.Count > 0 ? string.Join("、", focusTables.Take(2)) : "核心主题";
        var measuresText = focusMeasures.Count > 0 ? string.Join("、", focusMeasures.Take(2)) : "关键指标";

        return storyRole switch
        {
            StoryRole.Overview => $"{displayName} 先给出 {measuresText}，再总览 {tablesText} 的整体情况。",
            StoryRole.Trend => $"{displayName} 重点展示 {measuresText} 的趋势变化，并围绕 {tablesText} 展开。",
            StoryRole.Detail => $"{displayName} 以明细视角展示 {tablesText} 与 {measuresText}。",
            StoryRole.Breakdown => $"{displayName} 从拆解视角展示 {tablesText} 对 {measuresText} 的影响。",
            _ => $"{displayName} 围绕 {tablesText} 和 {measuresText} 提供页面内容。"
        };
    }

    private static bool HasVisual(IReadOnlyDictionary<string, int> visualTypeCounts, string pattern)
    {
        return visualTypeCounts.Keys.Any(key => key.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetVisualWeight(string visualType)
    {
        if (visualType.Contains("card", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (visualType.Contains("line", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }
}

public sealed record ResolvedPageData(
    StoryRole StoryRole,
    IReadOnlyList<string> FocusTables,
    IReadOnlyList<string> FocusMeasures,
    string BusinessQuestion,
    string NarrativeSummary);
