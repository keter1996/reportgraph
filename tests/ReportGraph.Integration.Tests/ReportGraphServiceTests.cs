using ReportGraph.Core.Models;
using ReportGraph.Core.Services;
using ReportGraph.HostIntegration.Services;
using ReportGraph.Storage.Storage;

namespace ReportGraph.Integration.Tests;

public sealed class ReportGraphServiceTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "ReportGraphIntegrationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BuildAsync_ReturnsGraphWithoutWritingFiles()
    {
        var input = CreateBuildInput(Path.Combine(tempRoot, "BuildOnly"));
        var service = CreateService();

        var graph = await service.BuildAsync(input);

        Assert.Equal("Sales", graph.Report.ReportName);
        Assert.False(Directory.Exists(Path.Combine(input.Source.PbipProjectPath, "Graph")));
    }

    [Fact]
    public async Task RefreshAsync_WritesGraphManifestAndContextFiles()
    {
        var input = CreateBuildInput(Path.Combine(tempRoot, "RefreshProject"));
        var service = CreateService();

        await service.RefreshAsync(input);

        var graphDirectory = Path.Combine(input.Source.PbipProjectPath, "Graph");
        Assert.True(File.Exists(Path.Combine(graphDirectory, "report-graph.json")));
        Assert.True(File.Exists(Path.Combine(graphDirectory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(graphDirectory, "context", "report-summary.md")));
        Assert.True(File.Exists(Path.Combine(graphDirectory, "context", "model.md")));
        Assert.True(File.Exists(Path.Combine(graphDirectory, "context", "bindings.md")));
        Assert.True(File.Exists(Path.Combine(graphDirectory, "context", "pages", "Page1.md")));
    }

    [Fact]
    public async Task LoadAsync_LoadsPreviouslyWrittenGraph()
    {
        var input = CreateBuildInput(Path.Combine(tempRoot, "LoadProject"));
        var service = CreateService();

        await service.RefreshAsync(input);
        var loaded = await service.LoadAsync(input.Source.PbipProjectPath);

        Assert.NotNull(loaded);
        Assert.Equal("Sales", loaded!.Report.ReportName);
    }

    [Fact]
    public async Task RefreshIfStaleAsync_ReusesGraphWhenFingerprintsMatch()
    {
        var projectPath = Path.Combine(tempRoot, "ReuseProject");
        var input = CreateBuildInput(projectPath);
        var service = CreateService();

        var firstGraph = await service.RefreshAsync(input);
        var graphPath = Path.Combine(projectPath, "Graph", "report-graph.json");
        var before = await File.ReadAllTextAsync(graphPath);

        var secondGraph = await service.RefreshIfStaleAsync(input);
        var after = await File.ReadAllTextAsync(graphPath);

        Assert.Equal(firstGraph.Report.ReportName, secondGraph.Report.ReportName);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task DeleteAsync_RemovesGraphDirectory()
    {
        var input = CreateBuildInput(Path.Combine(tempRoot, "DeleteProject"));
        var service = CreateService();
        await service.RefreshAsync(input);

        await service.DeleteAsync(input.Source.PbipProjectPath);

        Assert.False(Directory.Exists(Path.Combine(input.Source.PbipProjectPath, "Graph")));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static IReportGraphService CreateService()
    {
        return new ReportGraphService(
            builder: new ReportGraphBuilder(),
            renderer: new ReportGraphContextRenderer(),
            fingerprintService: new ReportGraphFingerprintService(),
            stalenessChecker: new ReportGraphStalenessChecker(),
            fileStore: new ReportGraphFileStore(),
            contextFileStore: new ReportGraphContextFileStore());
    }

    private static ReportGraphBuildInput CreateBuildInput(string projectPath)
    {
        return new ReportGraphBuildInput(
            Version: "1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            Source: new ReportGraphSource(
                InstanceId: "instance-1",
                PbipProjectPath: projectPath,
                ReportRootPath: Path.Combine(projectPath, "Report"),
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
                                ])
                        ])
                ]),
            Model: new SemanticModelInput(
                ModelName: "Sales Model",
                Tables:
                [
                    new TableInput("FactSales", false, ["SalesId"], ["Sales Amount"])
                ],
                Relationships: []));
    }
}
