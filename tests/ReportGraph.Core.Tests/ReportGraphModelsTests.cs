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
            Diagnostics: new ReportGraphDiagnostics(
                Warnings: [],
                Notes: ["Seed graph for serialization test."]));

        var json = JsonSerializer.Serialize(graph);

        Assert.Contains("\"Version\":\"1.0\"", json);
        Assert.Contains("\"StoryRole\":0", json);
        Assert.Contains("\"SemanticRole\":0", json);
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
            IsStale: false);

        var json = JsonSerializer.Serialize(manifest);

        Assert.Contains("\"GraphBuilderVersion\":\"0.1.0\"", json);
        Assert.Contains("\"IsStale\":false", json);
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

        Assert.Equal("SalesModel|12|156|34|21", modelFingerprint);
        Assert.Equal("2026-06-03T10:20:00.0000000+00:00|6|42", reportFingerprint);
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
            IsStale: false);

        Assert.False(checker.IsStale(manifest, "sales|1|1|1|0", "pages|1|1"));
        Assert.True(checker.IsStale(manifest, "sales|2|1|1|0", "pages|1|1"));
        Assert.True(checker.IsStale(manifest, "sales|1|1|1|0", "pages|2|1"));
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
                ]));

        var graph = builder.Build(input);

        Assert.Equal(["Page1", "Page2"], graph.Report.Pages.Select(page => page.PageId).ToArray());
        Assert.Equal(["Visual1", "Visual2", "Visual3"], graph.Bindings.VisualToFields.Select(binding => binding.VisualId).ToArray());
        Assert.Equal(["DimDate", "FactSales"], graph.Bindings.PageToTables.Single(binding => binding.PageId == "Page1").Tables);
        Assert.Equal(["Sales Amount"], graph.Bindings.PageToMeasures.Single(binding => binding.PageId == "Page1").Measures);
        Assert.Equal(["DimRegion", "FactSales"], graph.Bindings.PageToTables.Single(binding => binding.PageId == "Page2").Tables);
        Assert.Equal(["Margin"], graph.Bindings.PageToMeasures.Single(binding => binding.PageId == "Page2").Measures);
        Assert.Equal(3, graph.Model.Tables.Count);
        Assert.Single(graph.Model.Relationships);
        Assert.Equal("Page1", graph.Report.ActivePageId);
        Assert.Equal(StoryRole.Overview, graph.Report.Pages.Single(page => page.PageId == "Page1").StoryRole);
        Assert.Equal(StoryRole.Detail, graph.Report.Pages.Single(page => page.PageId == "Page2").StoryRole);
        Assert.Equal(SemanticRole.Fact, graph.Model.Tables.Single(table => table.Name == "FactSales").SemanticRole);
        Assert.Equal(SemanticRole.Dimension, graph.Model.Tables.Single(table => table.Name == "DimDate").SemanticRole);
        Assert.Equal(["FactSales", "DimDate"], graph.Report.Pages.Single(page => page.PageId == "Page1").FocusTables);
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
        Assert.Contains("FactSales", context.ModelSummary.Content);
        Assert.Contains("Page1/Visual1", context.BindingsSummary.Content);
        Assert.Contains("# Overview", context.PageSummaries.Single(page => page.RelativePath == "pages/Page1.md").Content);
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
                ]));
    }
}
