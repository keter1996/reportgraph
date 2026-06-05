using System.Text.Json;
using ReportGraph.Core.Models;
using ReportGraph.Core.Services;
using GraphModel = ReportGraph.Core.Models.ReportGraph;
using GraphManifest = ReportGraph.Core.Models.ReportGraphManifest;

namespace ReportGraph.Core.Tests;

public sealed class ReportGraphModelsTests
{
    [Fact]
    public void ReportGraph_MinimalInstance_CanSerialize()
    {
        var graph = new GraphModel(
            Version: "1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            Source: new ReportGraphSource(
                InstanceId: "instance-1",
                PbipProjectPath: @"D:\Example\Project.pbip",
                ReportRootPath: @"D:\Example\Report",
                ModelName: "Sales Model"),
            Report: new ReportGraphReportLayer(
                ReportName: "Sales",
                ActivePageId: "ReportSection1",
                Pages:
                [
                    new ReportGraphPageNode(
                        PageId: "ReportSection1",
                        DisplayName: "Overview",
                        Ordinal: 0,
                        StoryRole: StoryRole.Overview,
                        DominantVisualTypes: ["card"],
                        FocusTables: ["FactSales"],
                        FocusMeasures: ["Sales Amount"],
                        BusinessQuestion: "How are sales performing?",
                        NarrativeSummary: "High-level sales summary.")
                ],
                Storyline:
                [
                    new ReportGraphStoryStep(
                        StepId: "step-1",
                        PageId: "ReportSection1",
                        Ordinal: 0,
                        StoryRole: StoryRole.Overview,
                        Summary: "Start from the overall KPI view.")
                ]),
            Model: new ReportGraphModelLayer(
                Summary: new ReportGraphModelOverview(
                    ModelName: "Sales Model",
                    TableCount: 1,
                    ColumnCount: 1,
                    MeasureCount: 1,
                    RelationshipCount: 0),
                Tables:
                [
                    new ReportGraphTableNode(
                        Name: "FactSales",
                        SemanticRole: SemanticRole.Fact,
                        IsHidden: false,
                        ColumnCount: 1,
                        MeasureCount: 1,
                        RelationshipDegree: 0,
                        UsedByVisualCount: 1,
                        UsedByPages: ["ReportSection1"],
                        Measures: ["Sales Amount"])
                ],
                Relationships: []),
            Bindings: new ReportGraphBindingLayer(
                PageToTables:
                [
                    new ReportGraphPageTableBinding(
                        PageId: "ReportSection1",
                        Tables: ["FactSales"])
                ],
                PageToMeasures:
                [
                    new ReportGraphPageMeasureBinding(
                        PageId: "ReportSection1",
                        Measures: ["Sales Amount"])
                ],
                VisualToFields:
                [
                    new ReportGraphVisualFieldBinding(
                        PageId: "ReportSection1",
                        VisualId: "Visual1",
                        VisualType: "card",
                        Fields:
                        [
                            new ReportGraphFieldReference(
                                Role: "Value",
                                Table: "FactSales",
                                Field: "Sales Amount",
                                Kind: FieldReferenceKind.Measure)
                        ])
                ]),
            Semantics: new ReportGraphSemanticLayer(
                BusinessGlossary: new ReportGraphBusinessGlossary(
                    Terms:
                    [
                        new ReportGraphBusinessTerm(
                            TermId: "sales-amount",
                            DisplayName: "Sales Amount",
                            Aliases: ["Revenue"],
                            Description: "Primary sales metric.",
                            Unit: "currency",
                            CanonicalName: "Sales",
                            MappedObjects:
                            [
                                new ReportGraphSemanticObjectReference(
                                    Kind: SemanticObjectKind.Measure,
                                    Name: "Sales Amount",
                                    Table: "FactSales")
                            ])
                    ]),
                PageIntent: new ReportGraphPageIntentLayer(
                    Pages:
                    [
                        new ReportGraphPageIntentNode(
                            PageId: "ReportSection1",
                            Topic: "Sales performance",
                            PrimaryQuestion: "How are sales performing?",
                            ReadingOrder: ["kpi", "trend"],
                            PrimaryVisualIds: ["Visual1"],
                            VisualRoles:
                            [
                                new ReportGraphVisualRoleNode(
                                    VisualId: "Visual1",
                                    VisualRole: VisualRole.Kpi)
                            ])
                    ]),
                MeasureSemantics: new ReportGraphMeasureSemanticLayer(
                    Measures:
                    [
                        new ReportGraphMeasureSemanticNode(
                            Table: "FactSales",
                            Name: "Sales Amount",
                            BusinessName: "Sales Amount",
                            FormulaPattern: MeasureFormulaPattern.Aggregate,
                            BusinessTopic: "sales",
                            DependsOnMeasures: [],
                            DependsOnColumns:
                            [
                                new ReportGraphMeasureDependencyReference(
                                    Table: "FactSales",
                                    Name: "Amount")
                            ],
                            IsCoreMetric: true,
                            Complexity: SemanticComplexity.Low)
                    ]),
                ContextSemantics: new ReportGraphContextSemanticLayer(
                    Pages:
                    [
                        new ReportGraphPageContextNode(
                            PageId: "ReportSection1",
                            DefaultFilters: [],
                            VisualFilters: [],
                            CommonSlicers:
                            [
                                new ReportGraphSemanticObjectReference(
                                    Kind: SemanticObjectKind.Column,
                                    Name: "Month",
                                    Table: "DimDate")
                            ],
                            HighImpactDimensions: ["DimDate"])
                    ]),
                DocumentIndex: new ReportGraphDocumentIndex(
                    Documents:
                    [
                        new ReportGraphDocumentNode(
                            DocumentId: "usage-doc",
                            Path: "docs/usage.md",
                            Title: "Usage",
                            Summary: "How to use the sales report graph.",
                            Keywords: ["sales", "query"],
                            TopicTags: ["usage"],
                            LinkedObjects:
                            [
                                new ReportGraphSemanticObjectReference(
                                    Kind: SemanticObjectKind.Command,
                                    Name: "query")
                            ],
                            Scope: "project",
                            Version: "1.0")
                    ])),
            Diagnostics: new ReportGraphDiagnostics(
                Warnings: [],
                Notes: ["Seed graph for serialization test."]));

        var json = JsonSerializer.Serialize(graph);

        Assert.Contains("\"Version\":\"1.0\"", json);
        Assert.Contains("\"StoryRole\":0", json);
        Assert.Contains("\"SemanticRole\":0", json);
        Assert.Contains("\"Semantics\":", json);
        Assert.Contains("\"VisualRole\":1", json);
        Assert.Contains("\"FormulaPattern\":0", json);
        Assert.Contains("\"Provenance\":1", json);
    }

    [Fact]
    public void ReportGraphManifest_MinimalInstance_CanSerialize()
    {
        var manifest = new GraphManifest(
            Version: "1.0",
            GraphBuilderVersion: "0.1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            PbipProjectPath: @"D:\Example\Project.pbip",
            ReportRootPath: @"D:\Example\Report",
            ModelFingerprint: "sales|1|1|1|0",
            ReportFingerprint: "pages|1|1",
            IsStale: false,
            SourceFingerprint: "sha256:manifest",
            SourceFiles:
            [
                new SourceArtifactInput(
                    Path: "Sales.pbip",
                    ContentHash: "sha256:file",
                    LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 1, 0, 0, TimeSpan.Zero))
            ],
            StaleReason: null);

        var json = JsonSerializer.Serialize(manifest);

        Assert.Contains("\"GraphBuilderVersion\":\"0.1.0\"", json);
        Assert.Contains("\"IsStale\":false", json);
        Assert.Contains("\"SourceFingerprint\":\"sha256:manifest\"", json);
        Assert.Contains("\"SourceFiles\":[", json);
    }

    [Fact]
    public void FingerprintService_CreatesStableFingerprints()
    {
        var service = new ReportGraphFingerprintService();

        var modelFingerprint = service.CreateModelFingerprint(
            new ModelFingerprintInput(
                ModelName: "SalesModel",
                TableCount: 12,
                ColumnCount: 156,
                MeasureCount: 34,
                RelationshipCount: 21));
        var reportFingerprint = service.CreateReportFingerprint(
            new ReportFingerprintInput(
                PagesLastWriteUtc: new DateTimeOffset(2026, 6, 3, 10, 20, 0, TimeSpan.Zero),
                PageCount: 6,
                VisualCount: 42));
        var sourceFingerprint = service.CreateSourceFingerprint(
            [
                new SourceArtifactInput(
                    Path: @"docs\sales.md",
                    ContentHash: "sha256:b",
                    LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero)),
                new SourceArtifactInput(
                    Path: "Sales.pbip",
                    ContentHash: "sha256:a",
                    LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 9, 0, 0, TimeSpan.Zero))
            ]);
        var reorderedSourceFingerprint = service.CreateSourceFingerprint(
            [
                new SourceArtifactInput(
                    Path: "Sales.pbip",
                    ContentHash: "sha256:a",
                    LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero)),
                new SourceArtifactInput(
                    Path: "docs/sales.md",
                    ContentHash: "sha256:b",
                    LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 8, 0, 0, TimeSpan.Zero))
            ]);

        Assert.Equal("SalesModel|12|156|34|21", modelFingerprint);
        Assert.Equal("2026-06-03T10:20:00.0000000+00:00|6|42", reportFingerprint);
        Assert.NotNull(sourceFingerprint);
        Assert.Equal(sourceFingerprint, reorderedSourceFingerprint);
    }

    [Fact]
    public void StalenessChecker_DetectsFingerprintMismatch()
    {
        var checker = new ReportGraphStalenessChecker();
        var manifest = new GraphManifest(
            Version: "1.0",
            GraphBuilderVersion: "0.1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            PbipProjectPath: @"D:\Example\Project.pbip",
            ReportRootPath: @"D:\Example\Report",
            ModelFingerprint: "sales|1|1|1|0",
            ReportFingerprint: "pages|1|1",
            IsStale: false,
            SourceFingerprint: "sha256:source");

        Assert.False(checker.Evaluate(manifest, "sha256:source", "sales|1|1|1|0", "pages|1|1").IsStale);
        Assert.Equal("Source fingerprint changed", checker.Evaluate(manifest, "sha256:new", "sales|1|1|1|0", "pages|1|1").Reason);
        Assert.Equal("Model fingerprint changed", checker.Evaluate(manifest, "sha256:source", "sales|2|1|1|0", "pages|1|1").Reason);
        Assert.Equal("Report fingerprint changed", checker.Evaluate(manifest, "sha256:source", "sales|1|1|1|0", "pages|2|1").Reason);
    }

    [Fact]
    public void StalenessChecker_FallsBackToLegacyFingerprints_WhenSourceFingerprintIsUnavailable()
    {
        var checker = new ReportGraphStalenessChecker();
        var manifest = new GraphManifest(
            Version: "1.0",
            GraphBuilderVersion: "0.1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            PbipProjectPath: @"D:\Example\Project.pbip",
            ReportRootPath: @"D:\Example\Report",
            ModelFingerprint: "sales|1|1|1|0",
            ReportFingerprint: "pages|1|1",
            IsStale: false);

        var result = checker.Evaluate(manifest, null, "sales|1|1|1|0", "pages|1|1");

        Assert.False(result.IsStale);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void ReportGraphBuilder_BuildsBaseGraphFromAdapterNeutralInput()
    {
        var builder = new ReportGraphBuilder();
        var input = new ReportGraphBuildInput(
            Version: "1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            Source: new ReportGraphSource(
                InstanceId: "instance-1",
                PbipProjectPath: @"D:\Example\Project.pbip",
                ReportRootPath: @"D:\Example\Report",
                ModelName: "Sales Model"),
            Report: new ReportInput(
                ReportName: "Sales",
                ActivePageId: "Page1",
                PagesLastModifiedUtc: new DateTimeOffset(2026, 6, 3, 10, 20, 0, TimeSpan.Zero),
                Pages:
                [
                    new ReportPageInput(
                        PageId: "Page1",
                        DisplayName: "Overview",
                        Ordinal: 0,
                        Visuals:
                        [
                            new VisualInput(
                                VisualId: "Visual0",
                                VisualType: "slicer",
                                Fields:
                                [
                                    new VisualFieldInput("Values", "DimDate", "Month", FieldReferenceKind.Column)
                                ],
                                Filters:
                                [
                                    new VisualFilterInput("DimDate", "Month", ["Jan", "Feb"])
                                ]),
                            new VisualInput(
                                VisualId: "Visual1",
                                VisualType: "card",
                                Fields:
                                [
                                    new VisualFieldInput("Value", "FactSales", "Sales Amount", FieldReferenceKind.Measure)
                                ]),
                            new VisualInput(
                                VisualId: "Visual2",
                                VisualType: "linechart",
                                Fields:
                                [
                                    new VisualFieldInput("Category", "DimDate", "Month", FieldReferenceKind.Column),
                                    new VisualFieldInput("Y", "FactSales", "Sales Amount", FieldReferenceKind.Measure)
                                ])
                        ]),
                    new ReportPageInput(
                        PageId: "Page2",
                        DisplayName: "Region Detail",
                        Ordinal: 1,
                        Visuals:
                        [
                            new VisualInput(
                                VisualId: "Visual3",
                                VisualType: "table",
                                Fields:
                                [
                                    new VisualFieldInput("Category", "DimRegion", "Region", FieldReferenceKind.Column),
                                    new VisualFieldInput("Value", "FactSales", "Margin", FieldReferenceKind.Measure)
                                ])
                        ])
                ]),
            Model: new SemanticModelInput(
                ModelName: "Sales Model",
                Tables:
                [
                    new TableInput("FactSales", false, ["SalesId"], ["Sales Amount", "Margin", "Margin Rate"]),
                    new TableInput("DimDate", false, ["Month"], []),
                    new TableInput("DimRegion", false, ["Region"], [])
                ],
                Relationships:
                [
                    new RelationshipInput("rel-1", "FactSales", "DateKey", "DimDate", "DateKey", true)
                ],
                Columns:
                [
                    new ColumnInput("FactSales", "Amount", FormatString: "#,0"),
                    new ColumnInput("DimDate", "Month"),
                    new ColumnInput("DimRegion", "Region")
                ],
                Measures:
                [
                    new MeasureInput(
                        Table: "FactSales",
                        Name: "Sales Amount",
                        DisplayFolder: "Sales",
                        FormatString: "#,0",
                        Expression: "SUM('FactSales'[Amount])"),
                    new MeasureInput(
                        Table: "FactSales",
                        Name: "Margin",
                        DisplayFolder: "Profitability",
                        FormatString: "#,0",
                        Expression: "SUM('FactSales'[MarginAmount])"),
                    new MeasureInput(
                        Table: "FactSales",
                        Name: "Margin Rate",
                        DisplayFolder: "Profitability",
                        FormatString: "0.0%",
                        Expression: "DIVIDE([Margin], [Sales Amount])")
                ]),
            Documents:
            [
                new MarkdownDocumentInput(
                    Path: "docs/sales-overview.md",
                    Content:
                    """
                    # Sales Playbook

                    This document explains how to read the Overview page with Sales Amount and FactSales.

                    ## KPI usage
                    Use reportgraph query measure Sales Amount.
                    """,
                    LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero))
            ]);

        var graph = builder.Build(input);

        Assert.Equal(["Page1", "Page2"], graph.Report.Pages.Select(page => page.PageId).ToArray());
        Assert.Equal(["Visual0", "Visual1", "Visual2", "Visual3"], graph.Bindings.VisualToFields.Select(binding => binding.VisualId).ToArray());
        Assert.Equal(["DimDate", "FactSales"], graph.Bindings.PageToTables.Single(binding => binding.PageId == "Page1").Tables);
        Assert.Equal(["Sales Amount"], graph.Bindings.PageToMeasures.Single(binding => binding.PageId == "Page1").Measures);
        Assert.Equal(["DimRegion", "FactSales"], graph.Bindings.PageToTables.Single(binding => binding.PageId == "Page2").Tables);
        Assert.Equal(["Margin"], graph.Bindings.PageToMeasures.Single(binding => binding.PageId == "Page2").Measures);
        Assert.Equal(3, graph.Model.Tables.Count);
        Assert.Single(graph.Model.Relationships);
        Assert.Equal(["Profitability", "Sales"], graph.Semantics.BusinessGlossary.Terms.Select(term => term.DisplayName).ToArray());
        var salesTerm = graph.Semantics.BusinessGlossary.Terms.Single(term => term.DisplayName == "Sales");
        Assert.Contains("Sales Amount", salesTerm.Aliases);
        Assert.Contains(salesTerm.MappedObjects, reference => reference.Kind == SemanticObjectKind.Measure && reference.Name == "Sales Amount" && reference.Table == "FactSales");
        Assert.Contains(salesTerm.MappedObjects, reference => reference.Kind == SemanticObjectKind.Column && reference.Name == "Amount" && reference.Table == "FactSales");
        Assert.Equal(["Page1", "Page2"], graph.Semantics.PageIntent.Pages.Select(page => page.PageId).ToArray());
        var page1Intent = graph.Semantics.PageIntent.Pages.Single(page => page.PageId == "Page1");
        Assert.Equal("Overview", page1Intent.Topic);
        Assert.Equal("Overview 主要用于回答当前 Sales Amount 的整体表现如何。", page1Intent.PrimaryQuestion);
        Assert.Equal(["filter", "kpi", "trend"], page1Intent.ReadingOrder);
        Assert.Equal(["Visual1", "Visual2"], page1Intent.PrimaryVisualIds);
        Assert.Contains(page1Intent.VisualRoles, role => role.VisualId == "Visual0" && role.VisualRole == VisualRole.Filter);
        Assert.Contains(page1Intent.VisualRoles, role => role.VisualId == "Visual1" && role.VisualRole == VisualRole.Kpi);
        Assert.Contains(page1Intent.VisualRoles, role => role.VisualId == "Visual2" && role.VisualRole == VisualRole.Trend);
        Assert.Equal(["Margin", "Margin Rate", "Sales Amount"], graph.Semantics.MeasureSemantics.Measures.Select(measure => measure.Name).ToArray());
        var salesAmount = graph.Semantics.MeasureSemantics.Measures.Single(measure => measure.Name == "Sales Amount");
        Assert.Equal(MeasureFormulaPattern.Aggregate, salesAmount.FormulaPattern);
        Assert.Empty(salesAmount.DependsOnMeasures);
        Assert.Contains(salesAmount.DependsOnColumns, dependency => dependency.Table == "FactSales" && dependency.Name == "Amount");
        var marginRate = graph.Semantics.MeasureSemantics.Measures.Single(measure => measure.Name == "Margin Rate");
        Assert.Equal(MeasureFormulaPattern.Ratio, marginRate.FormulaPattern);
        Assert.Equal(["Margin", "Sales Amount"], marginRate.DependsOnMeasures.Select(dependency => dependency.Name).ToArray());
        Assert.Empty(marginRate.DependsOnColumns);
        Assert.Equal(["Page1", "Page2"], graph.Semantics.ContextSemantics.Pages.Select(page => page.PageId).ToArray());
        var page1Context = graph.Semantics.ContextSemantics.Pages.Single(page => page.PageId == "Page1");
        Assert.Equal(["Feb", "Jan"], page1Context.DefaultFilters.Select(filter => filter.Value!).ToArray());
        Assert.Single(page1Context.CommonSlicers);
        Assert.Contains(page1Context.CommonSlicers, slicer => slicer.Table == "DimDate" && slicer.Name == "Month" && slicer.VisualId == "Visual0");
        Assert.Equal(["DimDate"], page1Context.HighImpactDimensions);
        Assert.Equal(["Feb", "Jan"], page1Context.VisualFilters.Select(filter => filter.Value!).ToArray());
        var document = Assert.Single(graph.Semantics.DocumentIndex.Documents);
        Assert.Equal("docs-sales-overview-md", document.DocumentId);
        Assert.Equal("Sales Playbook", document.Title);
        Assert.Contains("Overview page", document.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(document.Keywords, keyword => string.Equals(keyword, "Sales", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(document.LinkedObjects, reference => reference.Kind == SemanticObjectKind.Page && reference.PageId == "Page1");
        Assert.Contains(document.LinkedObjects, reference => reference.Kind == SemanticObjectKind.Measure && reference.Name == "Sales Amount" && reference.Table == "FactSales");
        Assert.Contains(document.LinkedObjects, reference => reference.Kind == SemanticObjectKind.Table && reference.Name == "FactSales");
        Assert.Contains(document.LinkedObjects, reference => reference.Kind == SemanticObjectKind.Term && reference.Name == "Sales");
        Assert.Contains(document.LinkedObjects, reference => reference.Kind == SemanticObjectKind.Command && reference.Name == "reportgraph");
        Assert.Equal("Page1", graph.Report.ActivePageId);
        Assert.Equal(StoryRole.Overview, graph.Report.Pages.Single(page => page.PageId == "Page1").StoryRole);
        Assert.Equal(StoryRole.Detail, graph.Report.Pages.Single(page => page.PageId == "Page2").StoryRole);
        Assert.Equal(SemanticRole.Fact, graph.Model.Tables.Single(table => table.Name == "FactSales").SemanticRole);
        Assert.Equal(SemanticRole.Dimension, graph.Model.Tables.Single(table => table.Name == "DimDate").SemanticRole);
        Assert.Equal(["DimDate", "FactSales"], graph.Report.Pages.Single(page => page.PageId == "Page1").FocusTables);
        Assert.Equal(["Sales Amount"], graph.Report.Pages.Single(page => page.PageId == "Page1").FocusMeasures);
        Assert.NotNull(graph.Report.Pages.Single(page => page.PageId == "Page1").NarrativeSummary);
    }

    [Fact]
    public void ContextRenderer_RendersMarkdownDocuments()
    {
        var builder = new ReportGraphBuilder();
        var renderer = new ReportGraphContextRenderer();
        var graph = builder.Build(CreateBuildInput());

        var context = renderer.Render(graph);

        Assert.Equal("report-summary.md", context.ReportSummary.RelativePath);
        Assert.Equal("model.md", context.ModelSummary.RelativePath);
        Assert.Equal("bindings.md", context.BindingsSummary.RelativePath);
        Assert.Equal(2, context.PageSummaries.Count);
        Assert.Contains("# Sales", context.ReportSummary.Content);
        Assert.Contains("## Storyline", context.ReportSummary.Content);
        Assert.Contains("## Business Glossary", context.ReportSummary.Content);
        Assert.Contains("## Source Documents", context.ReportSummary.Content);
        Assert.Contains("FactSales", context.ModelSummary.Content);
        Assert.Contains("## Measure Semantics", context.ModelSummary.Content);
        Assert.Contains("Page1/Visual1", context.BindingsSummary.Content);
        var pageContext = context.PageSummaries.Single(page => page.RelativePath == "pages/Page1.md").Content;
        Assert.Contains("# Overview", pageContext);
        Assert.Contains("## Page Intent", pageContext);
        Assert.Contains("## Semantic Context", pageContext);
    }

    private static ReportGraphBuildInput CreateBuildInput()
    {
        return new ReportGraphBuildInput(
            Version: "1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            Source: new ReportGraphSource(
                InstanceId: "instance-1",
                PbipProjectPath: @"D:\Example\Project.pbip",
                ReportRootPath: @"D:\Example\Report",
                ModelName: "Sales Model"),
            Report: new ReportInput(
                ReportName: "Sales",
                ActivePageId: "Page1",
                PagesLastModifiedUtc: new DateTimeOffset(2026, 6, 3, 10, 20, 0, TimeSpan.Zero),
                Pages:
                [
                    new ReportPageInput(
                        PageId: "Page1",
                        DisplayName: "Overview",
                        Ordinal: 0,
                        Visuals:
                        [
                            new VisualInput(
                                VisualId: "Visual0",
                                VisualType: "slicer",
                                Fields:
                                [
                                    new VisualFieldInput("Values", "DimDate", "Month", FieldReferenceKind.Column)
                                ],
                                Filters:
                                [
                                    new VisualFilterInput("DimDate", "Month", ["Jan", "Feb"])
                                ]),
                            new VisualInput(
                                VisualId: "Visual1",
                                VisualType: "card",
                                Fields:
                                [
                                    new VisualFieldInput("Value", "FactSales", "Sales Amount", FieldReferenceKind.Measure)
                                ]),
                            new VisualInput(
                                VisualId: "Visual2",
                                VisualType: "linechart",
                                Fields:
                                [
                                    new VisualFieldInput("Category", "DimDate", "Month", FieldReferenceKind.Column),
                                    new VisualFieldInput("Y", "FactSales", "Sales Amount", FieldReferenceKind.Measure)
                                ])
                        ]),
                    new ReportPageInput(
                        PageId: "Page2",
                        DisplayName: "Region Detail",
                        Ordinal: 1,
                        Visuals:
                        [
                            new VisualInput(
                                VisualId: "Visual3",
                                VisualType: "table",
                                Fields:
                                [
                                    new VisualFieldInput("Category", "DimRegion", "Region", FieldReferenceKind.Column),
                                    new VisualFieldInput("Value", "FactSales", "Margin", FieldReferenceKind.Measure)
                                ])
                        ])
                ]),
            Model: new SemanticModelInput(
                ModelName: "Sales Model",
                Tables:
                [
                    new TableInput("FactSales", false, ["SalesId"], ["Sales Amount", "Margin"]),
                    new TableInput("DimDate", false, ["Month"], []),
                    new TableInput("DimRegion", false, ["Region"], [])
                ],
                Relationships:
                [
                    new RelationshipInput("rel-1", "FactSales", "DateKey", "DimDate", "DateKey", true)
                ],
                Columns:
                [
                    new ColumnInput("FactSales", "Amount")
                ],
                Measures:
                [
                    new MeasureInput(
                        Table: "FactSales",
                        Name: "Sales Amount",
                        DisplayFolder: "Sales",
                        FormatString: "#,0",
                        Expression: "SUM('FactSales'[Amount])")
                ]),
            Documents:
            [
                new MarkdownDocumentInput(
                    Path: "docs/sales.md",
                    Content:
                    """
                    # Sales Notes

                    Overview page notes for Sales Amount.
                    """,
                    LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero))
            ]);
    }
}
