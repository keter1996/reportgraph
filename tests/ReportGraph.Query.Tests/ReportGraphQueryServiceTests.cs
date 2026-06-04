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
        Assert.Equal(3, result.Visuals.Count);
    }

    [Fact]
    public void GetPageIntent_ReturnsSemanticPageIntent()
    {
        var graph = CreateGraph();

        var result = service.GetPageIntent(graph, "Page1");

        Assert.NotNull(result);
        Assert.Equal("Overview", result!.Topic);
        Assert.Contains("Sales Amount", result.PrimaryQuestion, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["filter", "kpi", "trend"], result.ReadingOrder);
    }

    [Fact]
    public void GetPageContext_ReturnsSemanticPageContext()
    {
        var graph = CreateGraph();

        var result = service.GetPageContext(graph, "Page1");

        Assert.NotNull(result);
        Assert.Equal(["Jan", "Mar"], result!.DefaultFilters.Select(filter => filter.Value!).ToArray());
        Assert.Contains(result.CommonSlicers, slicer => slicer.Table == "DimDate" && slicer.Name == "Month");
        Assert.Equal(["DimDate"], result.HighImpactDimensions);
    }

    [Fact]
    public void GetMeasure_ReturnsSemanticMeasureNode()
    {
        var graph = CreateGraph();

        var result = service.GetMeasure(graph, "Margin Rate", "FactSales");

        Assert.NotNull(result);
        Assert.Equal("FactSales", result!.Table);
        Assert.Equal(MeasureFormulaPattern.Ratio, result.FormulaPattern);
        Assert.Equal(["Margin", "Sales Amount"], result.DependsOnMeasures.Select(item => item.Name).ToArray());
    }

    [Fact]
    public void GetMeasureLineage_ReturnsRecursiveDependencyGraph()
    {
        var graph = CreateGraph();

        var result = service.GetMeasureLineage(graph, "Margin Rate", "FactSales");

        Assert.NotNull(result);
        Assert.Equal("Margin Rate", result!.Root.Name);
        Assert.Equal(["Margin", "Margin Rate", "Sales Amount"], result.Measures.Select(item => item.Name).ToArray());
        Assert.Contains(result.MeasureEdges, edge => edge.FromMeasure == "Margin Rate" && edge.ToMeasure == "Margin");
        Assert.Contains(result.MeasureEdges, edge => edge.FromMeasure == "Margin Rate" && edge.ToMeasure == "Sales Amount");
        Assert.Contains(result.ColumnEdges, edge => edge.FromMeasure == "Margin" && edge.ToColumn == "MarginAmount");
        Assert.Contains(result.ColumnEdges, edge => edge.FromMeasure == "Sales Amount" && edge.ToColumn == "SalesId");
    }

    [Fact]
    public void SearchTerms_ReturnsBusinessGlossaryMatches()
    {
        var graph = CreateGraph();

        var result = service.SearchTerms(graph, "Sales Amount");

        var match = Assert.Single(result.Matches);
        Assert.Equal("Sales", match.Term.DisplayName);
        Assert.Equal("alias", match.MatchedBy);
        Assert.Contains(match.Term.MappedObjects, reference => reference.Kind == SemanticObjectKind.Measure && reference.Name == "Sales Amount");
    }

    [Fact]
    public void GetDocument_ReturnsIndexedMarkdownDocument()
    {
        var graph = CreateGraph();

        var result = service.GetDocument(graph, "docs/sales-playbook.md");

        Assert.NotNull(result);
        Assert.Equal("Sales Playbook", result!.Title);
        Assert.Contains(result.LinkedObjects, reference => reference.Kind == SemanticObjectKind.Measure && reference.Name == "Sales Amount");
        Assert.Contains(result.LinkedObjects, reference => reference.Kind == SemanticObjectKind.Page && reference.PageId == "Page1");
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
                                    VisualId: "Visual0",
                                    VisualType: "slicer",
                                    Fields:
                                    [
                                        new VisualFieldInput("Values", "DimDate", "Month", FieldReferenceKind.Column)
                                    ],
                                    Filters:
                                    [
                                        new VisualFilterInput("DimDate", "Month", ["Jan", "Mar"])
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
                    Measures:
                    [
                        new MeasureInput(
                            Table: "FactSales",
                            Name: "Sales Amount",
                            DisplayFolder: "Sales",
                            FormatString: "#,0",
                            Expression: "SUM('FactSales'[SalesId])"),
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
                        Path: "docs/sales-playbook.md",
                        Content:
                        """
                        # Sales Playbook

                        The Overview page explains Sales Amount trends for FactSales.
                        """,
                        LastModifiedUtc: new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero))
                ]));
    }
}
