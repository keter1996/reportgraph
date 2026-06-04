using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using ReportGraph.Adapters.Services;
using ReportGraph.Cli;
using ReportGraph.Core.Services;
using ReportGraph.Distribution.Services;
using ReportGraph.HostIntegration.Services;
using ReportGraph.Query.Services;
using ReportGraph.Storage.Storage;

namespace ReportGraph.Cli.Tests;

public sealed class CliRunnerTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "ReportGraphCliRunnerTests", Guid.NewGuid().ToString("N"));
    private readonly string originalCurrentDirectory = Directory.GetCurrentDirectory();

    [Fact]
    public async Task RunAsync_InitAndStatus_ShouldUseCurrentDirectoryByDefault()
    {
        var projectPath = await CreateTmdlProjectAsync("CurrentDirectoryProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();

        var initExitCode = await runner.RunAsync(["init"]);
        var statusOutput = await CaptureConsoleOutAsync(() => runner.RunAsync(["status"]));

        Assert.Equal(0, initExitCode);
        Assert.Contains("Graph exists: True", statusOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Report name: Sales", statusOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Query_ShouldUseCurrentDirectoryWhenProjectPathIsOmitted()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryCurrentDirectoryProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["query", "page", "Page1"]));

        Assert.Contains("\"pageId\": \"Page1\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"displayName\": \"Overview\"", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Delete_ShouldRemoveGraphDirectory()
    {
        var projectPath = await CreateTmdlProjectAsync("DeleteProject");
        var runner = CreateRunner();
        await runner.RunAsync(["init", projectPath]);

        var deleteExitCode = await runner.RunAsync(["delete", projectPath]);

        Assert.Equal(0, deleteExitCode);
        Assert.False(Directory.Exists(Path.Combine(projectPath, "Graph")));
    }

    [Fact]
    public async Task RunAsync_Mcp_ShouldExposeInitializeAndToolsList()
    {
        var cliExecutablePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ReportGraph.Cli", "bin", "Debug", "net10.0", "reportgraph.exe"));

        await using var client = await CreateMcpClientAsync(cliExecutablePath, Directory.GetCurrentDirectory());
        var tools = await client.ListToolsAsync();

        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.graph.load", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.graph.explore", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("2025-06-18", client.ServerCapabilities is not null ? "2025-06-18" : null);
    }

    [Fact]
    public async Task RunAsync_McpToolCall_ShouldReturnStructuredContent()
    {
        var projectPath = await CreateTmdlProjectAsync("McpToolCallProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);
        var cliExecutablePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ReportGraph.Cli", "bin", "Debug", "net10.0", "reportgraph.exe"));

        await using var client = await CreateMcpClientAsync(cliExecutablePath, projectPath);
        var result = await client.CallToolAsync(
            "report.page.get",
            new Dictionary<string, object?>
            {
                ["projectRoot"] = projectPath,
                ["pageId"] = "Page1"
            });

        var json = JsonSerializer.Serialize(result);
        Assert.Contains("Page1", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Overview", json, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(originalCurrentDirectory);
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static CliRunner CreateRunner()
    {
        return new CliRunner(
            buildInputAdapter: new ReportGraphProjectAdapter(),
            distributionHost: new ReportGraphDistributionHost(),
            graphService: new ReportGraphService(
                builder: new ReportGraphBuilder(),
                renderer: new ReportGraphContextRenderer(),
                fingerprintService: new ReportGraphFingerprintService(),
                stalenessChecker: new ReportGraphStalenessChecker(),
                fileStore: new ReportGraphFileStore(),
                contextFileStore: new ReportGraphContextFileStore()),
            queryService: new ReportGraphQueryService(),
            fileStore: new ReportGraphFileStore());
    }

    private async Task<string> CreateTmdlProjectAsync(string projectName)
    {
        var projectPath = Path.Combine(tempRoot, projectName);
        var reportDirectoryPath = Path.Combine(projectPath, "Sales.Report");
        var semanticDefinitionPath = Path.Combine(projectPath, "Sales.SemanticModel", "definition");
        var tablesDirectoryPath = Path.Combine(semanticDefinitionPath, "tables");
        var pagesDirectoryPath = Path.Combine(reportDirectoryPath, "definition", "pages");
        var visualDirectoryPath = Path.Combine(pagesDirectoryPath, "Page1", "visuals", "Visual1");

        Directory.CreateDirectory(visualDirectoryPath);
        Directory.CreateDirectory(tablesDirectoryPath);

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "Sales.pbip"),
            """
            {
              "version": "1.0",
              "artifacts": [
                {
                  "report": {
                    "path": "Sales.Report"
                  }
                }
              ]
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(reportDirectoryPath, "definition.pbir"),
            """
            {
              "version": "4.0",
              "datasetReference": {
                "byPath": {
                  "path": "../Sales.SemanticModel"
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pagesDirectoryPath, "pages.json"),
            """
            {
              "pageOrder": ["Page1"],
              "activePageName": "Page1"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pagesDirectoryPath, "Page1", "page.json"),
            """
            {
              "name": "Page1",
              "displayName": "Overview"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(visualDirectoryPath, "visual.json"),
            """
            {
              "name": "Visual1",
              "visual": {
                "visualType": "card",
                "query": {
                  "queryState": {
                    "Value": {
                      "projections": [
                        {
                          "queryRef": "FactSales[Sales Amount]"
                        }
                      ]
                    }
                  }
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(semanticDefinitionPath, "database.tmdl"),
            "database 'Sales Model'");

        await File.WriteAllTextAsync(
            Path.Combine(tablesDirectoryPath, "FactSales.tmdl"),
            """
            table FactSales
                column SalesId
                measure 'Sales Amount' = SUM ( FactSales[SalesId] )
            """);

        return projectPath;
    }

    private static async Task<string> CaptureConsoleOutAsync(Func<Task<int>> action)
    {
        var writer = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            var exitCode = await action();
            Assert.Equal(0, exitCode);
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static async Task<McpClient> CreateMcpClientAsync(string cliExecutablePath, string workingDirectory)
    {
        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "ReportGraph CLI Test Client",
                Command = cliExecutablePath,
                Arguments = ["mcp"],
                WorkingDirectory = workingDirectory,
                ShutdownTimeout = TimeSpan.FromSeconds(5)
            },
            NullLoggerFactory.Instance);

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "reportgraph-cli-tests",
                    Version = "1.0.0"
                },
                ProtocolVersion = "2025-06-18",
                Capabilities = new ClientCapabilities(),
                InitializationTimeout = TimeSpan.FromSeconds(10)
            },
            NullLoggerFactory.Instance);
    }

}
