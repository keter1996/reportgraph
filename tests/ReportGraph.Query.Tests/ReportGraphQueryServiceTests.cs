using ReportGraph.Core.Models;
using ReportGraph.Core.Services;
using ReportGraph.Query.Services;

namespace ReportGraph.Query.Tests;

public sealed class ReportGraphQueryServiceTests
{
    private readonly IReportGraphQueryService service = new ReportGraphQueryService();

    [Fact]
    public void GetGraph_ReturnsGraph()
    {
        var graph = CreateGraph();

        var result = service.GetGraph(graph);

        Assert.Same(graph, result);
    }

    [Fact]
    public void GetPage_ReturnsPageSummary()
    {
        var graph = CreateGraph();

        var page = service.GetPage(graph, "Page1");

        Assert.NotNull(page);
        Assert.Equal("Overview", page!.DisplayName);
    }

    [Fact]
    public void GetPageBindings_ReturnsTablesMeasuresAndVisuals()
    {
        var graph = CreateGraph();

        var result = service.GetPageBindings(graph, "Page1");

        Assert.NotNull(result);
        Assert.Equal(["DimDate", "FactSales"], result!.Tables);
        Assert.Equal(["Sales Amount"], result.Measures);
        Assert.Equal(2, result.Visuals.Count);
    }

    [Fact]
    public void GetTableUsage_ReturnsRelatedPagesAndVisuals()
    {
        var graph = CreateGraph();

        var result = service.GetTableUsage(graph, "FactSales");

        Assert.NotNull(result);
        Assert.Equal("FactSales", result!.Table.Name);
        Assert.Equal(3, result.Visuals.Count);
    }

    [Fact]
    public void GetVisual_ReturnsVisualBinding()
    {
        var graph = CreateGraph();

        var result = service.GetVisual(graph, "Page1", "Visual2");

        Assert.NotNull(result);
        Assert.Equal("linechart", result!.VisualType);
    }

    [Fact]
    public void Explore_ReturnsPageOrTablePerspective()
    {
        var graph = CreateGraph();

        var fromPage = service.Explore(graph, new ExploreQuery(ExploreMode.FromPage, "Page1"));
        var fromTable = service.Explore(graph, new ExploreQuery(ExploreMode.FromTable, "FactSales"));

        Assert.Equal(["table:DimDate", "table:FactSales"], fromPage.Items);
        Assert.Equal(["page:Page1", "page:Page2"], fromTable.Items);
    }

    private static ReportGraph.Core.Models.ReportGraph CreateGraph()
    {
        var builder = new ReportGraphBuilder();

        return builder.Build(
            new ReportGraphBuildInput(
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
                    ])));
    }
}
