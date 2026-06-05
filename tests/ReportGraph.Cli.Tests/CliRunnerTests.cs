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
    public async Task RunAsync_Query_ShouldAutoRefreshWhenGraphIsMissing()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryAutoRefreshMissingProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();

        var result = await CaptureConsoleAsync(() => runner.RunAsync(["query", "page", "Page1"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"pageId\": \"Page1\"", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Auto refreshed graph: Manifest missing.", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(projectPath, "Graph", "report-graph.json")));
    }

    [Fact]
    public async Task RunAsync_Query_ShouldAutoRefreshWhenTrackedSourceChanges()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryAutoRefreshStaleProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "docs", "sales-playbook.md"),
            """
            # Sales Playbook

            The Overview page now explains Sales Amount and Margin Rate.
            """);

        var result = await CaptureConsoleAsync(() => runner.RunAsync(["query", "document", "docs/sales-playbook.md"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"title\": \"Sales Playbook\"", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Auto refreshed graph: Source fingerprint changed.", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Query_ShouldAutoRefreshWhenDirtyMarkExists()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryAutoRefreshDirtyProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);
        await runner.RunAsync(["mark-dirty", "--reason", "Marked dirty by test"]);

        var result = await CaptureConsoleAsync(() => runner.RunAsync(["query", "page", "Page1"]));
        var statusAfterQuery = await CaptureConsoleOutAsync(() => runner.RunAsync(["status"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"pageId\": \"Page1\"", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Auto refreshed graph: Marked dirty by test.", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dirty mark exists: False", statusAfterQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Doctor_ShouldReportProjectStructure()
    {
        var projectPath = await CreateTmdlProjectAsync("DoctorProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["doctor"]));

        Assert.Contains("ReportGraph Doctor", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PBIP files: 1", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Semantic model directories: 1", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Graph stale: True", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stale reason: Manifest missing", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Status: OK", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Status_ShouldNotAutoRefreshWhenGraphIsMissing()
    {
        var projectPath = await CreateTmdlProjectAsync("StatusNoRefreshProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["status"]));

        Assert.Contains("Graph file exists: False", output, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(projectPath, "Graph", "report-graph.json")));
    }

    [Fact]
    public async Task RunAsync_MarkDirty_ShouldPersistDirtyStateWithoutRefreshing()
    {
        var projectPath = await CreateTmdlProjectAsync("MarkDirtyProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["mark-dirty", "--reason", "Marked dirty by test"]));
        var statusOutput = await CaptureConsoleOutAsync(() => runner.RunAsync(["status"]));

        Assert.Contains("Marked graph dirty", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dirty mark exists: True", statusOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stale reason: Marked dirty by test", statusOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Doctor_ShouldExplainSourceFingerprintChanges()
    {
        var projectPath = await CreateTmdlProjectAsync("DoctorStaleProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "docs", "sales-playbook.md"),
            """
            # Sales Playbook

            The Overview page now explains Sales Amount and Top 10 sales ranking.
            """);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["doctor"]));

        Assert.Contains("Graph stale: True", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stale reason: Source fingerprint changed", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_QueryPageIntent_ShouldReturnSemanticPayload()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryPageIntentProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["query", "page-intent", "Page1"]));

        Assert.Contains("\"pageId\": \"Page1\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"topic\": \"Overview\"", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_QueryPageContext_ShouldReturnContextPayload()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryPageContextProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["query", "page-context", "Page1"]));

        Assert.Contains("\"pageId\": \"Page1\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"commonSlicers\"", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_QueryMeasureLineage_ShouldReturnLineagePayload()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryMeasureLineageProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["query", "measure-lineage", "Margin Rate", "FactSales"]));

        Assert.Contains("\"root\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"measureEdges\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sales Amount", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_QueryTermSearch_ShouldReturnGlossaryMatches()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryTermSearchProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["query", "term-search", "Sales Amount"]));

        Assert.Contains("\"matches\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sales Amount", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_QueryDocument_ShouldReturnMarkdownDocumentIndexNode()
    {
        var projectPath = await CreateTmdlProjectAsync("QueryDocumentProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);

        var output = await CaptureConsoleOutAsync(() => runner.RunAsync(["query", "document", "docs/sales-playbook.md"]));

        Assert.Contains("\"title\": \"Sales Playbook\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"linkedObjects\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sales Amount", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Init_ShouldRejectPbixPath()
    {
        var projectPath = await CreateTmdlProjectAsync("PbixInitProject");
        var pbixPath = Path.Combine(projectPath, "Sales.pbix");
        await File.WriteAllTextAsync(pbixPath, "fake-pbix");
        var runner = CreateRunner();

        var result = await CaptureConsoleErrorAndExitCodeAsync(() => runner.RunAsync(["init", pbixPath]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("PBIX is not a supported input", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PBIP", result.Error, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.graph.status", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.graph.mark_dirty", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.graph.explore", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.page.intent", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.page.context", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.measure.get", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.measure.lineage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.term.search", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, tool => string.Equals(tool.Name, "report.document.get", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task RunAsync_McpStatusToolCall_ShouldReturnStaleAndDirtyState()
    {
        var projectPath = await CreateTmdlProjectAsync("McpStatusToolCallProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);
        await runner.RunAsync(["mark-dirty", "--reason", "Marked dirty by test"]);
        var cliExecutablePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ReportGraph.Cli", "bin", "Debug", "net10.0", "reportgraph.exe"));

        await using var client = await CreateMcpClientAsync(cliExecutablePath, projectPath);
        var result = await client.CallToolAsync(
            "report.graph.status",
            new Dictionary<string, object?>
            {
                ["projectRoot"] = projectPath
            });

        var json = JsonSerializer.Serialize(result);
        Assert.Contains("dirtyMarkExists", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Marked dirty by test", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("graphStale", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_McpMarkDirtyToolCall_ShouldPersistDirtyState()
    {
        var projectPath = await CreateTmdlProjectAsync("McpMarkDirtyToolCallProject");
        Directory.SetCurrentDirectory(projectPath);
        var runner = CreateRunner();
        await runner.RunAsync(["init"]);
        var cliExecutablePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ReportGraph.Cli", "bin", "Debug", "net10.0", "reportgraph.exe"));

        await using var client = await CreateMcpClientAsync(cliExecutablePath, projectPath);
        var markDirtyResult = await client.CallToolAsync(
            "report.graph.mark_dirty",
            new Dictionary<string, object?>
            {
                ["projectRoot"] = projectPath,
                ["reason"] = "Marked dirty by MCP test"
            });
        var statusResult = await client.CallToolAsync(
            "report.graph.status",
            new Dictionary<string, object?>
            {
                ["projectRoot"] = projectPath
            });

        var markDirtyJson = JsonSerializer.Serialize(markDirtyResult);
        var statusJson = JsonSerializer.Serialize(statusResult);
        Assert.Contains("Marked dirty by MCP test", markDirtyJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Marked dirty by MCP test", statusJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dirtyMarkExists", statusJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_McpToolCall_ShouldAutoRefreshWhenGraphIsMissing()
    {
        var projectPath = await CreateTmdlProjectAsync("McpAutoRefreshProject");
        Directory.SetCurrentDirectory(projectPath);
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
        Assert.True(File.Exists(Path.Combine(projectPath, "Graph", "report-graph.json")));
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
        var visual1DirectoryPath = Path.Combine(pagesDirectoryPath, "Page1", "visuals", "Visual1");
        var visual0DirectoryPath = Path.Combine(pagesDirectoryPath, "Page1", "visuals", "Visual0");

        Directory.CreateDirectory(visual1DirectoryPath);
        Directory.CreateDirectory(visual0DirectoryPath);
        Directory.CreateDirectory(tablesDirectoryPath);
        Directory.CreateDirectory(Path.Combine(projectPath, "docs"));

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
            Path.Combine(visual0DirectoryPath, "visual.json"),
            """
            {
              "name": "Visual0",
              "visual": {
                "visualType": "slicer",
                "query": {
                  "queryState": {
                    "Values": {
                      "projections": [
                        {
                          "queryRef": "DimDate.Month"
                        }
                      ]
                    }
                  }
                },
                "objects": {
                  "general": [
                    {
                      "properties": {
                        "filter": {
                          "filter": {
                            "Version": 2,
                            "From": [
                              {
                                "Name": "d",
                                "Entity": "DimDate",
                                "Type": 0
                              }
                            ],
                            "Where": [
                              {
                                "Condition": {
                                  "In": {
                                    "Expressions": [
                                      {
                                        "Column": {
                                          "Expression": {
                                            "SourceRef": {
                                              "Source": "d"
                                            }
                                          },
                                          "Property": "Month"
                                        }
                                      }
                                    ],
                                    "Values": [
                                      [
                                        {
                                          "Literal": {
                                            "Value": "'Jan'"
                                          }
                                        }
                                      ]
                                    ]
                                  }
                                }
                              }
                            ]
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(visual1DirectoryPath, "visual.json"),
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
            Path.Combine(tablesDirectoryPath, "DimDate.tmdl"),
            """
            table DimDate
                column Month
            """);

        await File.WriteAllTextAsync(
            Path.Combine(tablesDirectoryPath, "FactSales.tmdl"),
            """
            table FactSales
                column SalesId
                measure 'Sales Amount' = SUM ( FactSales[SalesId] )
                    displayFolder: Sales
                measure Margin = SUM ( FactSales[SalesId] )
                    displayFolder: Profitability
                measure 'Margin Rate' = DIVIDE ( [Margin], [Sales Amount] )
                    displayFolder: Profitability
            """);

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "docs", "sales-playbook.md"),
            """
            # Sales Playbook

            The Overview page explains Sales Amount for FactSales.

            ## CLI
            Use reportgraph query measure Sales Amount.
            """);

        return projectPath;
    }

    private static async Task<string> CaptureConsoleOutAsync(Func<Task<int>> action)
    {
        var result = await CaptureConsoleAsync(action);
        Assert.Equal(0, result.ExitCode);
        return result.Output;
    }

    private static async Task<(int ExitCode, string Error)> CaptureConsoleErrorAndExitCodeAsync(Func<Task<int>> action)
    {
        var result = await CaptureConsoleAsync(action);
        return (result.ExitCode, result.Error);
    }

    private static async Task<(int ExitCode, string Output, string Error)> CaptureConsoleAsync(Func<Task<int>> action)
    {
        var outWriter = new StringWriter();
        var errorWriter = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            var exitCode = await action();
            return (exitCode, outWriter.ToString(), errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
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
