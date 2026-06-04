using System.Text.RegularExpressions;
using ReportGraph.Core.Models;

namespace ReportGraph.Core.Services;

public interface IReportGraphSemanticBuilder
{
    ReportGraphSemanticLayer Build(ReportGraphBuildInput input, IReadOnlyList<ReportGraphPageNode> pages);
}

public sealed partial class ReportGraphSemanticBuilder : IReportGraphSemanticBuilder
{
    public ReportGraphSemanticLayer Build(ReportGraphBuildInput input, IReadOnlyList<ReportGraphPageNode> pages)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(pages);

        var businessGlossary = BuildBusinessGlossary(input.Model);

        return new ReportGraphSemanticLayer(
            BusinessGlossary: businessGlossary,
            PageIntent: BuildPageIntent(input.Report.Pages, pages),
            MeasureSemantics: BuildMeasureSemantics(input.Model, businessGlossary),
            ContextSemantics: BuildContextSemantics(input.Report.Pages),
            DocumentIndex: BuildDocumentIndex(input, pages, businessGlossary));
    }

    private static ReportGraphBusinessGlossary BuildBusinessGlossary(SemanticModelInput model)
    {
        var measures = model.Measures ?? [];
        if (measures.Count == 0)
        {
            return ReportGraphBusinessGlossary.Empty;
        }

        var columnsByIdentity = (model.Columns ?? [])
            .GroupBy(column => CreateIdentity(column.Table, column.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var terms = measures
            .Where(measure => !string.IsNullOrWhiteSpace(measure.DisplayFolder))
            .GroupBy(measure => NormalizeTermId(measure.DisplayFolder!), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var canonicalName = group.First().DisplayFolder!.Trim();
                var mappedObjects = new List<ReportGraphSemanticObjectReference>();
                var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var measure in group.OrderBy(item => item.Table, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    mappedObjects.Add(new ReportGraphSemanticObjectReference(
                        Kind: SemanticObjectKind.Measure,
                        Name: measure.Name,
                        Table: measure.Table));

                    if (!string.Equals(measure.Name, canonicalName, StringComparison.OrdinalIgnoreCase))
                    {
                        aliases.Add(measure.Name);
                    }

                    foreach (var dependency in ExtractColumnDependencies(measure.Expression))
                    {
                        mappedObjects.Add(new ReportGraphSemanticObjectReference(
                            Kind: SemanticObjectKind.Column,
                            Name: dependency.Field,
                            Table: dependency.Table));

                        if (!string.Equals(dependency.Field, canonicalName, StringComparison.OrdinalIgnoreCase))
                        {
                            aliases.Add(dependency.Field);
                        }

                        if (columnsByIdentity.TryGetValue(CreateIdentity(dependency.Table, dependency.Field), out var column) &&
                            !string.IsNullOrWhiteSpace(column.DisplayFolder) &&
                            !string.Equals(column.DisplayFolder, canonicalName, StringComparison.OrdinalIgnoreCase))
                        {
                            aliases.Add(column.DisplayFolder);
                        }
                    }
                }

                return new ReportGraphBusinessTerm(
                    TermId: group.Key,
                    DisplayName: canonicalName,
                    Aliases: aliases.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                    Description: $"Business glossary term derived from semantic model display folder '{canonicalName}'.",
                    Unit: InferUnit(group),
                    CanonicalName: canonicalName,
                    MappedObjects: mappedObjects
                        .Distinct()
                        .OrderBy(item => item.Kind)
                        .ThenBy(item => item.Table, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    Provenance: Provenance.Derived);
            })
            .OrderBy(term => term.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return terms.Length == 0
            ? ReportGraphBusinessGlossary.Empty
            : new ReportGraphBusinessGlossary(terms);
    }

    private static ReportGraphPageIntentLayer BuildPageIntent(
        IReadOnlyList<ReportPageInput> inputPages,
        IReadOnlyList<ReportGraphPageNode> pages)
    {
        var pageNodeById = pages.ToDictionary(page => page.PageId, StringComparer.OrdinalIgnoreCase);

        var pageIntentNodes = inputPages
            .OrderBy(page => page.Ordinal)
            .Select(page =>
            {
                pageNodeById.TryGetValue(page.PageId, out var pageNode);
                var visualRoles = page.Visuals
                    .OrderBy(visual => visual.VisualId, StringComparer.OrdinalIgnoreCase)
                    .Select(visual => new ReportGraphVisualRoleNode(
                        VisualId: visual.VisualId,
                        VisualRole: ClassifyVisualRole(visual.VisualType),
                        Provenance: Provenance.Derived))
                    .ToArray();

                var readingOrder = visualRoles
                    .Select(role => role.VisualRole)
                    .Distinct()
                    .OrderBy(GetVisualRolePriority)
                    .Select(role => role.ToString().ToLowerInvariant())
                    .ToArray();

                var primaryVisualIds = page.Visuals
                    .Select(visual => new
                    {
                        visual.VisualId,
                        Role = ClassifyVisualRole(visual.VisualType),
                        Score = GetVisualPriorityScore(ClassifyVisualRole(visual.VisualType))
                    })
                    .Where(visual => visual.Role is not VisualRole.Filter
                        and not VisualRole.Navigation
                        and not VisualRole.Annotation
                        and not VisualRole.Decoration
                        and not VisualRole.Unknown)
                    .OrderByDescending(visual => visual.Score)
                    .ThenBy(visual => visual.VisualId, StringComparer.OrdinalIgnoreCase)
                    .Select(visual => visual.VisualId)
                    .Take(3)
                    .ToArray();

                return new ReportGraphPageIntentNode(
                    PageId: page.PageId,
                    Topic: page.DisplayName,
                    PrimaryQuestion: pageNode?.BusinessQuestion,
                    ReadingOrder: readingOrder,
                    PrimaryVisualIds: primaryVisualIds,
                    VisualRoles: visualRoles,
                    Provenance: Provenance.Derived);
            })
            .ToArray();

        return pageIntentNodes.Length == 0
            ? ReportGraphPageIntentLayer.Empty
            : new ReportGraphPageIntentLayer(pageIntentNodes);
    }

    private static ReportGraphMeasureSemanticLayer BuildMeasureSemantics(
        SemanticModelInput model,
        ReportGraphBusinessGlossary businessGlossary)
    {
        var measures = model.Measures ?? [];
        if (measures.Count == 0)
        {
            return ReportGraphMeasureSemanticLayer.Empty;
        }

        var measureIdentityLookup = measures
            .GroupBy(measure => CreateIdentity(measure.Table, measure.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var measureNameLookup = measures
            .GroupBy(measure => measure.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var termsByMeasureIdentity = businessGlossary.Terms
            .SelectMany(term => term.MappedObjects
                .Where(reference => reference.Kind == SemanticObjectKind.Measure && !string.IsNullOrWhiteSpace(reference.Table))
                .Select(reference => new
                {
                    Identity = CreateIdentity(reference.Table!, reference.Name),
                    Term = term
                }))
            .GroupBy(item => item.Identity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Term, StringComparer.OrdinalIgnoreCase);

        var semanticNodes = measures
            .OrderBy(measure => measure.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(measure => measure.Name, StringComparer.OrdinalIgnoreCase)
            .Select(measure =>
            {
                var dependencies = ExtractMeasureDependencies(
                    measure,
                    measureIdentityLookup,
                    measureNameLookup);

                var term = termsByMeasureIdentity.GetValueOrDefault(CreateIdentity(measure.Table, measure.Name));
                var dependencyCount = dependencies.Measures.Count + dependencies.Columns.Count;

                return new ReportGraphMeasureSemanticNode(
                    Table: measure.Table,
                    Name: measure.Name,
                    BusinessName: term?.DisplayName ?? measure.Name,
                    FormulaPattern: InferFormulaPattern(measure, dependencies.Measures, dependencies.Columns),
                    BusinessTopic: term?.CanonicalName ?? measure.DisplayFolder,
                    DependsOnMeasures: dependencies.Measures,
                    DependsOnColumns: dependencies.Columns,
                    IsCoreMetric: dependencies.Measures.Count == 0,
                    Complexity: InferComplexity(dependencyCount),
                    Provenance: Provenance.Derived);
            })
            .ToArray();

        return semanticNodes.Length == 0
            ? ReportGraphMeasureSemanticLayer.Empty
            : new ReportGraphMeasureSemanticLayer(semanticNodes);
    }

    private static ReportGraphContextSemanticLayer BuildContextSemantics(
        IReadOnlyList<ReportPageInput> inputPages)
    {
        var pageContexts = inputPages
            .OrderBy(page => page.Ordinal)
            .Select(page =>
            {
                var defaultFilters = page.Visuals
                    .Where(visual => ClassifyVisualRole(visual.VisualType) == VisualRole.Filter)
                    .SelectMany(visual => CreateFilterContextNodes(visual, includeVisualId: false))
                    .Distinct()
                    .OrderBy(filter => filter.Table, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(filter => filter.Field, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(filter => filter.Value, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var visualFilters = page.Visuals
                    .SelectMany(visual => CreateFilterContextNodes(visual, includeVisualId: true))
                    .OrderBy(filter => filter.VisualId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(filter => filter.Table, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(filter => filter.Field, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(filter => filter.Value, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var commonSlicers = page.Visuals
                    .Where(visual => ClassifyVisualRole(visual.VisualType) == VisualRole.Filter)
                    .SelectMany(visual => visual.Fields
                        .Where(field => field.Kind == FieldReferenceKind.Column)
                        .Select(field => new ReportGraphSemanticObjectReference(
                            Kind: SemanticObjectKind.Column,
                            Name: field.Field,
                            Table: field.Table,
                            PageId: page.PageId,
                            VisualId: visual.VisualId)))
                    .Distinct()
                    .OrderBy(reference => reference.Table, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(reference => reference.VisualId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var highImpactDimensions = InferHighImpactDimensions(page, defaultFilters, visualFilters);

                return new ReportGraphPageContextNode(
                    PageId: page.PageId,
                    DefaultFilters: defaultFilters,
                    VisualFilters: visualFilters,
                    CommonSlicers: commonSlicers,
                    HighImpactDimensions: highImpactDimensions,
                    Provenance: Provenance.Derived);
            })
            .ToArray();

        return pageContexts.Length == 0
            ? ReportGraphContextSemanticLayer.Empty
            : new ReportGraphContextSemanticLayer(pageContexts);
    }

    private static ReportGraphDocumentIndex BuildDocumentIndex(
        ReportGraphBuildInput input,
        IReadOnlyList<ReportGraphPageNode> pages,
        ReportGraphBusinessGlossary businessGlossary)
    {
        var documents = input.Documents ?? [];
        if (documents.Count == 0)
        {
            return ReportGraphDocumentIndex.Empty;
        }

        var documentNodes = documents
            .Where(document => !string.IsNullOrWhiteSpace(document.Path))
            .OrderBy(document => document.Path, StringComparer.OrdinalIgnoreCase)
            .Select(document =>
            {
                var title = ExtractMarkdownTitle(document);
                var headings = ExtractMarkdownHeadings(document.Content);
                var summary = ExtractMarkdownSummary(document.Content);
                var linkedObjects = LinkDocumentObjects(document, pages, input.Model, businessGlossary);
                var keywords = ExtractDocumentKeywords(title, headings, linkedObjects);
                var topicTags = ExtractDocumentTopicTags(headings, linkedObjects);

                return new ReportGraphDocumentNode(
                    DocumentId: NormalizeDocumentId(document.Path),
                    Path: document.Path,
                    Title: title,
                    Summary: summary,
                    Keywords: keywords,
                    TopicTags: topicTags,
                    LinkedObjects: linkedObjects,
                    Scope: InferDocumentScope(document.Path),
                    Version: null,
                    Provenance: Provenance.Derived);
            })
            .ToArray();

        return documentNodes.Length == 0
            ? ReportGraphDocumentIndex.Empty
            : new ReportGraphDocumentIndex(documentNodes);
    }

    private static string ExtractMarkdownTitle(MarkdownDocumentInput document)
    {
        foreach (var line in SplitMarkdownLines(document.Content))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return Path.GetFileNameWithoutExtension(document.Path);
    }

    private static IReadOnlyList<string> ExtractMarkdownHeadings(string content)
    {
        return SplitMarkdownLines(content)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('#'))
            .Select(line => line.TrimStart('#').Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static string? ExtractMarkdownSummary(string content)
    {
        foreach (var line in SplitMarkdownLines(content))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 ||
                trimmed.StartsWith('#') ||
                trimmed.StartsWith("```", StringComparison.Ordinal) ||
                trimmed.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            return trimmed.Length <= 240 ? trimmed : $"{trimmed[..240]}...";
        }

        return null;
    }

    private static IReadOnlyList<string> SplitMarkdownLines(string content)
    {
        return content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyList<ReportGraphSemanticObjectReference> LinkDocumentObjects(
        MarkdownDocumentInput document,
        IReadOnlyList<ReportGraphPageNode> pages,
        SemanticModelInput model,
        ReportGraphBusinessGlossary businessGlossary)
    {
        var haystack = $"{document.Path}\n{document.Content}";
        var references = new HashSet<ReportGraphSemanticObjectReference>();

        if (ContainsText(haystack, "reportgraph"))
        {
            references.Add(new ReportGraphSemanticObjectReference(SemanticObjectKind.Command, "reportgraph"));
        }

        foreach (var page in pages)
        {
            if (ContainsText(haystack, page.PageId) || ContainsText(haystack, page.DisplayName))
            {
                references.Add(new ReportGraphSemanticObjectReference(
                    Kind: SemanticObjectKind.Page,
                    Name: page.DisplayName,
                    PageId: page.PageId));
            }
        }

        foreach (var table in model.Tables)
        {
            if (ContainsText(haystack, table.Name))
            {
                references.Add(new ReportGraphSemanticObjectReference(SemanticObjectKind.Table, table.Name));
            }

            foreach (var column in table.Columns)
            {
                if (ContainsText(haystack, column))
                {
                    references.Add(new ReportGraphSemanticObjectReference(
                        Kind: SemanticObjectKind.Column,
                        Name: column,
                        Table: table.Name));
                }
            }

            foreach (var measure in table.Measures)
            {
                if (ContainsText(haystack, measure))
                {
                    references.Add(new ReportGraphSemanticObjectReference(
                        Kind: SemanticObjectKind.Measure,
                        Name: measure,
                        Table: table.Name));
                }
            }
        }

        foreach (var term in businessGlossary.Terms)
        {
            if (ContainsText(haystack, term.DisplayName) ||
                term.Aliases.Any(alias => ContainsText(haystack, alias)))
            {
                references.Add(new ReportGraphSemanticObjectReference(
                    Kind: SemanticObjectKind.Term,
                    Name: term.DisplayName));
            }
        }

        return references
            .OrderBy(reference => reference.Kind)
            .ThenBy(reference => reference.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.PageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractDocumentKeywords(
        string title,
        IReadOnlyList<string> headings,
        IReadOnlyList<ReportGraphSemanticObjectReference> linkedObjects)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddKeywordParts(keywords, title);

        foreach (var heading in headings.Take(5))
        {
            AddKeywordParts(keywords, heading);
        }

        foreach (var reference in linkedObjects.Take(10))
        {
            keywords.Add(reference.Name);
        }

        return keywords
            .Where(keyword => keyword.Length > 1)
            .OrderBy(keyword => keyword, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractDocumentTopicTags(
        IReadOnlyList<string> headings,
        IReadOnlyList<ReportGraphSemanticObjectReference> linkedObjects)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var heading in headings.Take(3))
        {
            tags.Add(heading);
        }

        foreach (var reference in linkedObjects.Where(reference => reference.Kind is SemanticObjectKind.Term or SemanticObjectKind.Page).Take(5))
        {
            tags.Add(reference.Name);
        }

        return tags
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static void AddKeywordParts(ISet<string> keywords, string value)
    {
        foreach (Match match in KeywordRegex().Matches(value))
        {
            keywords.Add(match.Value);
        }
    }

    private static string NormalizeDocumentId(string path)
    {
        var normalized = path
            .Replace('\\', '/')
            .Trim()
            .ToLowerInvariant();

        return DocumentIdUnsafeCharacterRegex().Replace(normalized, "-").Trim('-');
    }

    private static string InferDocumentScope(string path)
    {
        var normalized = path.Replace('\\', '/');
        var firstSegment = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstSegment) || string.Equals(firstSegment, Path.GetFileName(normalized), StringComparison.OrdinalIgnoreCase)
            ? "project"
            : firstSegment;
    }

    private static bool ContainsText(string source, string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static VisualRole ClassifyVisualRole(string visualType)
    {
        if (string.IsNullOrWhiteSpace(visualType))
        {
            return VisualRole.Unknown;
        }

        return visualType.Trim().ToLowerInvariant() switch
        {
            "slicer" => VisualRole.Filter,
            "card" => VisualRole.Kpi,
            "kpi" => VisualRole.Kpi,
            "linechart" => VisualRole.Trend,
            "linechartvisual" => VisualRole.Trend,
            "scatterchart" => VisualRole.Trend,
            "table" => VisualRole.Detail,
            "tableex" => VisualRole.Detail,
            "matrix" => VisualRole.Detail,
            "actionbutton" => VisualRole.Navigation,
            "textbox" => VisualRole.Annotation,
            "shape" => VisualRole.Decoration,
            "image" => VisualRole.Decoration,
            "columnchart" => VisualRole.Comparison,
            "clusteredcolumnchart" => VisualRole.Comparison,
            "barchart" => VisualRole.Comparison,
            _ when visualType.Contains("line", StringComparison.OrdinalIgnoreCase) => VisualRole.Trend,
            _ when visualType.Contains("column", StringComparison.OrdinalIgnoreCase) => VisualRole.Comparison,
            _ when visualType.Contains("bar", StringComparison.OrdinalIgnoreCase) => VisualRole.Comparison,
            _ => VisualRole.Unknown
        };
    }

    private static int GetVisualRolePriority(VisualRole role)
    {
        return role switch
        {
            VisualRole.Filter => 0,
            VisualRole.Kpi => 1,
            VisualRole.Trend => 2,
            VisualRole.Comparison => 3,
            VisualRole.Detail => 4,
            VisualRole.Navigation => 5,
            VisualRole.Annotation => 6,
            VisualRole.Decoration => 7,
            _ => 8
        };
    }

    private static int GetVisualPriorityScore(VisualRole role)
    {
        return role switch
        {
            VisualRole.Kpi => 100,
            VisualRole.Trend => 90,
            VisualRole.Comparison => 80,
            VisualRole.Detail => 70,
            VisualRole.Filter => 60,
            VisualRole.Navigation => 40,
            VisualRole.Annotation => 20,
            VisualRole.Decoration => 10,
            _ => 0
        };
    }

    private static string? InferUnit(IEnumerable<MeasureInput> measures)
    {
        foreach (var measure in measures)
        {
            if (measure.FormatString?.Contains('%', StringComparison.Ordinal) == true)
            {
                return "percent";
            }

            if (measure.DisplayFolder?.Contains("金额", StringComparison.OrdinalIgnoreCase) == true ||
                measure.Name.Contains("金额", StringComparison.OrdinalIgnoreCase))
            {
                return "currency";
            }
        }

        return null;
    }

    private static IReadOnlyList<string> InferHighImpactDimensions(
        ReportPageInput page,
        IReadOnlyList<ReportGraphFilterContextNode> defaultFilters,
        IReadOnlyList<ReportGraphFilterContextNode> visualFilters)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var visual in page.Visuals)
        {
            var role = ClassifyVisualRole(visual.VisualType);
            if (role is VisualRole.Unknown or VisualRole.Annotation or VisualRole.Decoration or VisualRole.Navigation)
            {
                continue;
            }

            foreach (var field in visual.Fields.Where(field => field.Kind == FieldReferenceKind.Column))
            {
                if (role != VisualRole.Filter && !IsHighImpactFieldRole(field.Role))
                {
                    continue;
                }

                AddScore(scores, field.Table, 2);

                if (role == VisualRole.Filter)
                {
                    AddScore(scores, field.Table, 3);
                }
                else if (string.Equals(field.Role, "Category", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(field.Role, "Axis", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(field.Role, "X", StringComparison.OrdinalIgnoreCase))
                {
                    AddScore(scores, field.Table, 1);
                }
            }
        }

        foreach (var filter in defaultFilters)
        {
            AddScore(scores, filter.Table, 2);
        }

        foreach (var filter in visualFilters)
        {
            AddScore(scores, filter.Table, 1);
        }

        return scores
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Key)
            .Take(5)
            .ToArray();
    }

    private static void AddScore(IDictionary<string, int> scores, string table, int delta)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            return;
        }

        scores[table] = scores.TryGetValue(table, out var current)
            ? current + delta
            : delta;
    }

    private static bool IsHighImpactFieldRole(string role)
    {
        return role.Equals("Category", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Axis", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("X", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Rows", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Columns", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Legend", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<ReportGraphFilterContextNode> CreateFilterContextNodes(VisualInput visual, bool includeVisualId)
    {
        var filters = visual.Filters ?? [];
        foreach (var filter in filters)
        {
            if (filter.Values.Count == 0)
            {
                yield return new ReportGraphFilterContextNode(
                    Scope: includeVisualId ? "visual" : "page",
                    VisualId: includeVisualId ? visual.VisualId : null,
                    Table: filter.Table,
                    Field: filter.Field);

                continue;
            }

            foreach (var value in filter.Values)
            {
                yield return new ReportGraphFilterContextNode(
                    Scope: includeVisualId ? "visual" : "page",
                    VisualId: includeVisualId ? visual.VisualId : null,
                    Table: filter.Table,
                    Field: filter.Field,
                    Value: value);
            }
        }
    }

    private static (
        IReadOnlyList<ReportGraphMeasureDependencyReference> Measures,
        IReadOnlyList<ReportGraphMeasureDependencyReference> Columns)
        ExtractMeasureDependencies(
            MeasureInput measure,
            IReadOnlyDictionary<string, MeasureInput> measureIdentityLookup,
            IReadOnlyDictionary<string, MeasureInput[]> measureNameLookup)
    {
        if (string.IsNullOrWhiteSpace(measure.Expression))
        {
            return ([], []);
        }

        var currentIdentity = CreateIdentity(measure.Table, measure.Name);
        var measureDependencies = new HashSet<ReportGraphMeasureDependencyReference>();
        var columnDependencies = new HashSet<ReportGraphMeasureDependencyReference>();

        foreach (var dependency in ExtractColumnDependencies(measure.Expression))
        {
            var dependencyIdentity = CreateIdentity(dependency.Table, dependency.Field);
            if (measureIdentityLookup.TryGetValue(dependencyIdentity, out _))
            {
                if (!string.Equals(dependencyIdentity, currentIdentity, StringComparison.OrdinalIgnoreCase))
                {
                    measureDependencies.Add(new ReportGraphMeasureDependencyReference(dependency.Table, dependency.Field));
                }

                continue;
            }

            columnDependencies.Add(new ReportGraphMeasureDependencyReference(dependency.Table, dependency.Field));
        }

        var normalizedExpression = ColumnReferenceRegex().Replace(measure.Expression, " ");
        foreach (Match match in BareMeasureReferenceRegex().Matches(normalizedExpression))
        {
            var referenceName = match.Groups["name"].Value.Trim();
            if (referenceName.Length == 0)
            {
                continue;
            }

            MeasureInput? referencedMeasure = null;
            if (measureIdentityLookup.TryGetValue(CreateIdentity(measure.Table, referenceName), out var sameTableMeasure))
            {
                referencedMeasure = sameTableMeasure;
            }
            else if (measureNameLookup.TryGetValue(referenceName, out var candidates) && candidates.Length == 1)
            {
                referencedMeasure = candidates[0];
            }

            if (referencedMeasure is null)
            {
                continue;
            }

            var referencedIdentity = CreateIdentity(referencedMeasure.Table, referencedMeasure.Name);
            if (string.Equals(referencedIdentity, currentIdentity, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            measureDependencies.Add(new ReportGraphMeasureDependencyReference(
                Table: referencedMeasure.Table,
                Name: referencedMeasure.Name));
        }

        return (
            measureDependencies
                .OrderBy(item => item.Table, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            columnDependencies
                .OrderBy(item => item.Table, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static SemanticComplexity InferComplexity(int dependencyCount)
    {
        return dependencyCount switch
        {
            <= 1 => SemanticComplexity.Low,
            <= 3 => SemanticComplexity.Medium,
            _ => SemanticComplexity.High
        };
    }

    private static MeasureFormulaPattern InferFormulaPattern(
        MeasureInput measure,
        IReadOnlyList<ReportGraphMeasureDependencyReference> measureDependencies,
        IReadOnlyList<ReportGraphMeasureDependencyReference> columnDependencies)
    {
        var expression = measure.Expression ?? string.Empty;
        var normalized = expression.ToUpperInvariant();
        var normalizedName = measure.Name.ToUpperInvariant();

        if (ContainsAny(normalized, "TOTALYTD", "TOTALQTD", "TOTALMTD", "SAMEPERIODLASTYEAR", "DATEADD", "DATESYTD", "DATESMTD", "DATESQTD", "PREVIOUSMONTH", "PREVIOUSYEAR"))
        {
            return MeasureFormulaPattern.TimeIntelligence;
        }

        if (ContainsAny(normalized, "RANKX(", "TOPN(") || ContainsAny(normalizedName, "RANK", "TOP"))
        {
            return MeasureFormulaPattern.Rank;
        }

        if (ContainsAny(normalized, "DIVIDE(", "/") || normalizedName.Contains("占比", StringComparison.Ordinal) || normalizedName.Contains("率", StringComparison.Ordinal))
        {
            return MeasureFormulaPattern.Ratio;
        }

        if (ContainsAny(normalizedName, "同比", "环比", "差异", "VARIANCE") || normalized.Contains(" - "))
        {
            return MeasureFormulaPattern.Variance;
        }

        if (ContainsAny(normalized, "IF(", "SWITCH("))
        {
            return MeasureFormulaPattern.Classification;
        }

        if (ContainsAny(normalized, "RUNNINGSUM(", "WINDOW(", "OFFSET("))
        {
            return MeasureFormulaPattern.RunningTotal;
        }

        if (columnDependencies.Count > 0 || measureDependencies.Count > 0 || ContainsAny(normalized, "SUM(", "SUMX(", "AVERAGE(", "AVERAGEX(", "COUNT(", "COUNTROWS(", "DISTINCTCOUNT(", "MIN(", "MAX("))
        {
            return MeasureFormulaPattern.Aggregate;
        }

        return MeasureFormulaPattern.Unknown;
    }

    private static bool ContainsAny(string source, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (source.Contains(pattern, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeTermId(string displayName)
    {
        return displayName
            .Trim()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("/", "-", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static string CreateIdentity(string table, string name)
    {
        return $"{table}::{name}";
    }

    private static IEnumerable<(string Table, string Field)> ExtractColumnDependencies(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            yield break;
        }

        foreach (Match match in ColumnReferenceRegex().Matches(expression))
        {
            var table = match.Groups["table"].Value.Trim();
            var field = match.Groups["field"].Value.Trim();
            if (table.Length == 0 || field.Length == 0)
            {
                continue;
            }

            yield return (table, field);
        }
    }

    [GeneratedRegex(@"'(?<table>[^']+)'\[(?<field>[^\]]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ColumnReferenceRegex();

    [GeneratedRegex(@"\[(?<name>[^\]]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BareMeasureReferenceRegex();

    [GeneratedRegex(@"[\p{L}\p{N}_\-\u4e00-\u9fff]+", RegexOptions.Compiled)]
    private static partial Regex KeywordRegex();

    [GeneratedRegex(@"[^a-z0-9\u4e00-\u9fff]+", RegexOptions.Compiled)]
    private static partial Regex DocumentIdUnsafeCharacterRegex();
}
