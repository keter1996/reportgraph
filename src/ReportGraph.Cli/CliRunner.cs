using ReportGraph.Adapters.Services;
using ReportGraph.Core.Models;
using ReportGraph.Distribution.Services;
using ReportGraph.HostIntegration.Services;
using ReportGraph.Query.Services;
using ReportGraph.Storage.Artifacts;
using ReportGraph.Storage.Storage;

namespace ReportGraph.Cli;

internal sealed class CliRunner
{
    private readonly IReportGraphBuildInputAdapter buildInputAdapter;
    private readonly IReportGraphDistributionHost distributionHost;
    private readonly IReportGraphService graphService;
    private readonly IReportGraphQueryService queryService;
    private readonly IReportGraphFileStore fileStore;

    public CliRunner(
        IReportGraphBuildInputAdapter buildInputAdapter,
        IReportGraphDistributionHost distributionHost,
        IReportGraphService graphService,
        IReportGraphQueryService queryService,
        IReportGraphFileStore fileStore)
    {
        this.buildInputAdapter = buildInputAdapter;
        this.distributionHost = distributionHost;
        this.graphService = graphService;
        this.queryService = queryService;
        this.fileStore = fileStore;
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (args.Count is 0 || IsHelp(args[0]))
        {
            Console.WriteLine(RenderHelp());
            return 0;
        }

        var command = args[0];
        var remaining = args.Skip(1).ToArray();

        try
        {
            var output = command.ToLowerInvariant() switch
            {
                "init" or "build" or "refresh" => await RunInitAsync(remaining, cancellationToken),
                "update" or "u" => await RunUpdateAsync(remaining, cancellationToken),
                "delete" or "remove" or "rm" => await RunDeleteAsync(remaining, cancellationToken),
                "status" or "st" => await RunStatusAsync(remaining, cancellationToken),
                "doctor" or "diag" or "validate" => await RunDoctorAsync(remaining, cancellationToken),
                "mark-dirty" or "dirty" or "notify" => await RunMarkDirtyAsync(remaining, cancellationToken),
                "query" or "q" => await RunQueryAsync(remaining, cancellationToken),
                "watch" => await RunWatchAsync(remaining, cancellationToken),
                "mcp" => await RunMcpAsync(remaining, cancellationToken),
                "install-info" or "info" => RenderInstallInfo(),
                _ => throw new ArgumentException(
                    $"Unknown command: {command}{Environment.NewLine}Run 'reportgraph help' to see the available commands.")
            };

            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine(output);
            }
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FileNotFoundException or DirectoryNotFoundException or NotSupportedException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private async Task<string> RunInitAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: reportgraph init [project-path-or-build-input-json]");
        }

        var targetPath = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
        var input = await buildInputAdapter.LoadAsync(targetPath, cancellationToken);
        var graph = await graphService.RefreshAsync(input, cancellationToken);
        var paths = ReportGraphPathResolver.Resolve(input.Source.PbipProjectPath);

        return string.Join(
            Environment.NewLine,
            [
                $"Initialized graph for report '{graph.Report.ReportName ?? "unknown"}'.",
                $"Project root: {input.Source.PbipProjectPath}",
                $"Graph directory: {paths.GraphDirectoryPath}"
            ]);
    }

    private async Task<string> RunUpdateAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("Usage: reportgraph update [project-path-or-build-input-json] [--force]");
        }

        var force = args.Any(arg => string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase));
        var pathArg = args.FirstOrDefault(arg => !string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase));
        var targetPath = pathArg ?? Directory.GetCurrentDirectory();

        var input = await buildInputAdapter.LoadAsync(targetPath, cancellationToken);
        var graph = force
            ? await graphService.RefreshAsync(input, cancellationToken)
            : await graphService.RefreshIfStaleAsync(input, cancellationToken);
        var paths = ReportGraphPathResolver.Resolve(input.Source.PbipProjectPath);

        return string.Join(
            Environment.NewLine,
            [
                $"{(force ? "Force updated" : "Updated")} graph for report '{graph.Report.ReportName ?? "unknown"}'.",
                $"Graph directory: {paths.GraphDirectoryPath}",
                $"Graph file: {paths.ReportGraphFilePath}",
                $"Manifest file: {paths.ManifestFilePath}"
            ]);
    }

    private async Task<string> RunDeleteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: reportgraph delete [project-path]");
        }

        var targetPath = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
        var projectPath = ResolveProjectPath(targetPath);
        var paths = ReportGraphPathResolver.Resolve(projectPath);

        await graphService.DeleteAsync(projectPath, cancellationToken);

        return string.Join(
            Environment.NewLine,
            [
                $"Deleted graph artifacts for '{projectPath}'.",
                $"Graph directory: {paths.GraphDirectoryPath}"
            ]);
    }

    private async Task<string> RunStatusAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: reportgraph status [project-path]");
        }

        var targetPath = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
        var input = await buildInputAdapter.LoadAsync(targetPath, cancellationToken);
        var projectPath = input.Source.PbipProjectPath;
        var paths = ReportGraphPathResolver.Resolve(projectPath);
        var graph = await graphService.LoadAsync(projectPath, cancellationToken);
        var refreshState = await graphService.EvaluateRefreshStateAsync(input, cancellationToken);
        var manifest = refreshState.Manifest;

        var lines = new List<string>
        {
            $"Project root: {projectPath}",
            $"Graph directory: {paths.GraphDirectoryPath}",
            $"Graph exists: {Directory.Exists(paths.GraphDirectoryPath)}",
            $"Graph file exists: {File.Exists(paths.ReportGraphFilePath)}",
            $"Manifest exists: {File.Exists(paths.ManifestFilePath)}",
            $"Dirty mark exists: {refreshState.DirtyState is not null}",
            $"Graph stale: {refreshState.IsStale}",
            $"Stale reason: {refreshState.Reason}",
            $"Source files tracked: {refreshState.SourceFileCount}"
        };

        if (manifest is not null)
        {
            lines.Add($"Graph version: {manifest.Version}");
            lines.Add($"Generated at: {manifest.GeneratedAtUtc:O}");
            lines.Add($"Model fingerprint: {manifest.ModelFingerprint}");
            lines.Add($"Report fingerprint: {manifest.ReportFingerprint}");
            lines.Add($"Source fingerprint: {manifest.SourceFingerprint ?? "n/a"}");
            lines.Add($"Source files tracked: {manifest.SourceFiles?.Count ?? 0}");
        }

        if (refreshState.DirtyState is not null)
        {
            lines.Add($"Dirty reason: {refreshState.DirtyState.Reason}");
            lines.Add($"Dirty marked at: {refreshState.DirtyState.MarkedAtUtc:O}");
        }

        if (graph is not null)
        {
            lines.Add($"Report name: {graph.Report.ReportName ?? "unknown"}");
            lines.Add($"Page count: {graph.Report.Pages.Count}");
            lines.Add($"Table count: {graph.Model.Tables.Count}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<string> RunDoctorAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: reportgraph doctor [project-path-or-pbip-file]");
        }

        var targetPath = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
        var input = await buildInputAdapter.LoadAsync(targetPath, cancellationToken);
        var projectRoot = input.Source.PbipProjectPath;
        var refreshState = await graphService.EvaluateRefreshStateAsync(input, cancellationToken);
        var pbipFiles = Directory.Exists(projectRoot)
            ? Directory.GetFiles(projectRoot, "*.pbip", SearchOption.TopDirectoryOnly)
            : [];
        var reportDirectories = Directory.Exists(projectRoot)
            ? Directory.GetDirectories(projectRoot, "*.Report", SearchOption.TopDirectoryOnly)
            : [];
        var semanticModelDirectories = Directory.Exists(projectRoot)
            ? Directory.GetDirectories(projectRoot, "*.SemanticModel", SearchOption.TopDirectoryOnly)
            : [];

        var lines = new List<string>
        {
            "ReportGraph Doctor",
            $"Input: {Path.GetFullPath(targetPath)}",
            $"Project root: {projectRoot}",
            $"Report root: {input.Source.ReportRootPath}",
            $"Model name: {input.Source.ModelName}",
            $"PBIP files: {pbipFiles.Length}",
            $"Report directories: {reportDirectories.Length}",
            $"Semantic model directories: {semanticModelDirectories.Length}",
            $"Pages: {input.Report.Pages.Count}",
            $"Tables: {input.Model.Tables.Count}",
            $"Markdown documents: {input.Documents?.Count ?? 0}",
            $"Source files tracked: {refreshState.SourceFileCount}",
            $"Graph directory exists: {refreshState.GraphDirectoryExists}",
            $"Graph file exists: {refreshState.GraphFileExists}",
            $"Manifest exists: {refreshState.ManifestExists}",
            $"Dirty mark exists: {refreshState.DirtyState is not null}",
            $"Graph stale: {refreshState.IsStale}",
            $"Stale reason: {refreshState.Reason}",
            $"Source fingerprint: {refreshState.SourceFingerprint ?? "n/a"}"
        };

        if (refreshState.DirtyState is not null)
        {
            lines.Add($"Dirty reason: {refreshState.DirtyState.Reason}");
            lines.Add($"Dirty marked at: {refreshState.DirtyState.MarkedAtUtc:O}");
        }

        if (pbipFiles.Length == 1)
        {
            lines.Add($"PBIP file: {pbipFiles[0]}");
        }
        else if (pbipFiles.Length == 0)
        {
            lines.Add("Recommendation: keep a .pbip file at the project root for the most stable install and host-integration experience.");
        }
        else
        {
            lines.Add("Recommendation: keep exactly one .pbip file at the project root to avoid ambiguous host entrypoints.");
        }

        lines.Add("Status: OK");
        return string.Join(Environment.NewLine, lines);
    }

    private async Task<string> RunMarkDirtyAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 3)
        {
            throw new ArgumentException("Usage: reportgraph mark-dirty [project-path] [--reason <reason>]");
        }

        var (pathArg, reason) = ParsePathAndReason(args, "Marked dirty by explicit host notification");
        var projectPath = ResolveProjectPath(pathArg ?? Directory.GetCurrentDirectory());
        await graphService.MarkDirtyAsync(projectPath, reason, cancellationToken);

        return string.Join(
            Environment.NewLine,
            [
                $"Marked graph dirty for '{projectPath}'.",
                $"Reason: {reason}"
            ]);
    }

    private async Task<string> RunQueryAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 1)
        {
            throw new ArgumentException(
                "Usage: reportgraph query [project-path] <graph|page|page-intent|page-context|page-bindings|measure|measure-lineage|term-search|document|table|visual|explore> [...]");
        }

        var queryCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "graph", "page", "page-intent", "page-context", "page-bindings", "measure", "measure-lineage", "term-search", "document", "table", "visual", "explore"
        };

        string projectPath;
        string[] effectiveArgs;
        if (queryCommands.Contains(args[0]))
        {
            projectPath = Directory.GetCurrentDirectory();
            effectiveArgs = args;
        }
        else
        {
            if (args.Length < 2)
            {
                throw new ArgumentException(
                    "Usage: reportgraph query [project-path] <graph|page|page-intent|page-context|page-bindings|measure|measure-lineage|term-search|document|table|visual|explore> [...]");
            }

            projectPath = ResolveProjectPath(args[0]);
            effectiveArgs = args[1..];
        }

        var ensuredGraph = await EnsureGraphForQueryAsync(projectPath, cancellationToken);
        var graph = ensuredGraph.Graph;

        var queryName = effectiveArgs[0].ToLowerInvariant();
        object? result = queryName switch
        {
            "graph" => queryService.GetGraph(graph),
            "page" when effectiveArgs.Length >= 2 => queryService.GetPage(graph, effectiveArgs[1]),
            "page-intent" when effectiveArgs.Length >= 2 => queryService.GetPageIntent(graph, effectiveArgs[1]),
            "page-context" when effectiveArgs.Length >= 2 => queryService.GetPageContext(graph, effectiveArgs[1]),
            "page-bindings" when effectiveArgs.Length >= 2 => queryService.GetPageBindings(graph, effectiveArgs[1]),
            "measure" when effectiveArgs.Length >= 2 => queryService.GetMeasure(
                graph,
                effectiveArgs[1],
                effectiveArgs.Length >= 3 ? effectiveArgs[2] : null),
            "measure-lineage" when effectiveArgs.Length >= 2 => queryService.GetMeasureLineage(
                graph,
                effectiveArgs[1],
                effectiveArgs.Length >= 3 ? effectiveArgs[2] : null),
            "term-search" when effectiveArgs.Length >= 2 => queryService.SearchTerms(graph, effectiveArgs[1]),
            "document" when effectiveArgs.Length >= 2 => queryService.GetDocument(graph, effectiveArgs[1]),
            "table" when effectiveArgs.Length >= 2 => queryService.GetTableUsage(graph, effectiveArgs[1]),
            "visual" when effectiveArgs.Length >= 3 => queryService.GetVisual(graph, effectiveArgs[1], effectiveArgs[2]),
            "explore" when effectiveArgs.Length >= 3 => queryService.Explore(graph, CreateExploreQuery(effectiveArgs[1], effectiveArgs[2])),
            _ => throw new ArgumentException(
                "Usage: reportgraph query [project-path] <graph|page|page-intent|page-context|page-bindings|measure|measure-lineage|term-search|document|table|visual|explore> [...]")
        };

        return ReportGraph.Storage.Serialization.ReportGraphJson.Serialize(result);
    }

    private async Task<string> RunMcpAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 0)
        {
            throw new ArgumentException("Usage: reportgraph mcp");
        }

        await Mcp.ReportGraphMcpHost.RunAsync(cancellationToken);
        return string.Empty;
    }

    private async Task<string> RunWatchAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 4)
        {
            throw new ArgumentException("Usage: reportgraph watch [project-path] [--refresh] [--debounce-ms <milliseconds>]");
        }

        var refreshOnChange = args.Any(arg => string.Equals(arg, "--refresh", StringComparison.OrdinalIgnoreCase));
        var debounceMilliseconds = ParseDebounceMilliseconds(args);
        var pathArg = args.FirstOrDefault(arg =>
            !string.Equals(arg, "--refresh", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(arg, "--debounce-ms", StringComparison.OrdinalIgnoreCase) &&
            !int.TryParse(arg, out _));
        var projectPath = ResolveProjectPath(pathArg ?? Directory.GetCurrentDirectory());

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler? handler = null;
        handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            linkedCancellationTokenSource.Cancel();
        };

        Console.CancelKeyPress += handler;

        var sync = new object();
        using var executionLock = new SemaphoreSlim(1, 1);
        CancellationTokenSource? pendingChangeTokenSource = null;
        Task pendingWork = Task.CompletedTask;

        void Schedule(string reason)
        {
            lock (sync)
            {
                pendingChangeTokenSource?.Cancel();
                pendingChangeTokenSource?.Dispose();
                pendingChangeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(linkedCancellationTokenSource.Token);
                var scheduledToken = pendingChangeTokenSource.Token;

                pendingWork = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(debounceMilliseconds, scheduledToken);
                        await executionLock.WaitAsync(scheduledToken);
                        try
                        {
                            if (refreshOnChange)
                            {
                                var input = await buildInputAdapter.LoadAsync(projectPath, scheduledToken);
                                await graphService.RefreshAsync(input, scheduledToken);
                                Console.WriteLine($"Refreshed graph from watch: {reason}");
                            }
                            else
                            {
                                await graphService.MarkDirtyAsync(projectPath, reason, scheduledToken);
                                Console.WriteLine($"Marked graph dirty from watch: {reason}");
                            }
                        }
                        finally
                        {
                            executionLock.Release();
                        }
                    }
                    catch (OperationCanceledException) when (scheduledToken.IsCancellationRequested)
                    {
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FileNotFoundException or DirectoryNotFoundException or NotSupportedException or IOException)
                    {
                        Console.Error.WriteLine($"Watch processing failed: {ex.Message}");
                    }
                }, CancellationToken.None);
            }
        }

        using var watcher = new FileSystemWatcher(projectPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        watcher.Changed += (_, eventArgs) => TryHandleWatchEvent(projectPath, eventArgs.FullPath, $"Changed {BuildWatchRelativePath(projectPath, eventArgs.FullPath)}", Schedule);
        watcher.Created += (_, eventArgs) => TryHandleWatchEvent(projectPath, eventArgs.FullPath, $"Created {BuildWatchRelativePath(projectPath, eventArgs.FullPath)}", Schedule);
        watcher.Deleted += (_, eventArgs) => TryHandleWatchEvent(projectPath, eventArgs.FullPath, $"Deleted {BuildWatchRelativePath(projectPath, eventArgs.FullPath)}", Schedule);
        watcher.Renamed += (_, eventArgs) =>
        {
            var oldRelativePath = BuildWatchRelativePath(projectPath, eventArgs.OldFullPath);
            var newRelativePath = BuildWatchRelativePath(projectPath, eventArgs.FullPath);
            if (ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, eventArgs.OldFullPath) ||
                ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, eventArgs.FullPath))
            {
                Schedule($"Renamed {oldRelativePath} -> {newRelativePath}");
            }
        };
        watcher.EnableRaisingEvents = true;

        Console.WriteLine($"Watching '{projectPath}' (refresh={refreshOnChange}, debounceMs={debounceMilliseconds}). Press Ctrl+C to stop.");

        try
        {
            await Task.Delay(Timeout.Infinite, linkedCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            watcher.EnableRaisingEvents = false;
            Console.CancelKeyPress -= handler;
            lock (sync)
            {
                pendingChangeTokenSource?.Cancel();
            }

            await pendingWork;
        }

        return "Stopped watch.";
    }

    private string RenderHelp()
    {
        var lines = new List<string>
        {
            "Report Graph CLI",
            string.Empty,
            "Commands:"
        };

        foreach (var command in distributionHost.GetCliCommands())
        {
            var aliases = command.Aliases.Count == 0
                ? string.Empty
                : $" (aliases: {string.Join(", ", command.Aliases)})";

            lines.Add($"  {command.Name}{aliases}");
            lines.Add($"    {command.Summary}");
            lines.Add($"    Usage: {command.Usage}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string RenderInstallInfo()
    {
        var guide = distributionHost.GetInstallationGuide();
        var lines = new List<string>
        {
            "Installation",
            $"  Clone: {guide.RepositoryCloneExample}",
            $"  Build: {guide.BuildCommand}",
            $"  CLI:   {guide.CliEntryPoint}",
            string.Empty,
            "Host Integration",
            $"  Codex: {guide.CodexIntegrationSummary}",
            $"  CloudCode: {guide.CloudCodeIntegrationSummary}",
            string.Empty,
            "MCP Tools"
        };

        foreach (var tool in distributionHost.GetMcpTools())
        {
            lines.Add($"  {tool.Name}");
            lines.Add($"    {tool.Summary}");
        }

        lines.Add(string.Empty);
        lines.Add("Notes");

        foreach (var note in guide.Notes)
        {
            lines.Add($"  - {note}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ExploreQuery CreateExploreQuery(string mode, string identifier)
    {
        var exploreMode = mode.ToLowerInvariant() switch
        {
            "from-page" => ExploreMode.FromPage,
            "from-table" => ExploreMode.FromTable,
            _ => throw new ArgumentException("Explore mode must be 'from-page' or 'from-table'.")
        };

        return new ExploreQuery(exploreMode, identifier);
    }

    private static bool IsHelp(string value) =>
        value.Equals("help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("-h", StringComparison.OrdinalIgnoreCase);

    private static string ResolveProjectPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return fullPath;
        }

        if (File.Exists(fullPath) && (
                string.Equals(Path.GetExtension(fullPath), ".pbip", StringComparison.OrdinalIgnoreCase)))
        {
            return Path.GetDirectoryName(fullPath)!;
        }

        return fullPath;
    }

    private async Task<ReportGraphResolvedGraph> EnsureGraphForQueryAsync(string targetPath, CancellationToken cancellationToken)
    {
        var input = await buildInputAdapter.LoadAsync(targetPath, cancellationToken);
        var resolved = await graphService.LoadOrRefreshAsync(input, cancellationToken);

        if (resolved.WasRefreshed)
        {
            Console.Error.WriteLine($"Auto refreshed graph: {resolved.RefreshState.Reason}.");
        }

        return resolved;
    }

    private static (string? Path, string Reason) ParsePathAndReason(string[] args, string defaultReason)
    {
        string? path = null;
        string reason = defaultReason;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--reason", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("The --reason option requires a value.");
                }

                reason = args[index + 1];
                index++;
                continue;
            }

            path ??= arg;
        }

        return (path, reason);
    }

    private static int ParseDebounceMilliseconds(string[] args)
    {
        const int defaultDebounceMilliseconds = 2000;

        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--debounce-ms", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var debounceMilliseconds) || debounceMilliseconds < 0)
            {
                throw new ArgumentException("The --debounce-ms option requires a non-negative integer value.");
            }

            return debounceMilliseconds;
        }

        return defaultDebounceMilliseconds;
    }

    private static void TryHandleWatchEvent(string projectPath, string filePath, string reason, Action<string> schedule)
    {
        if (ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, filePath))
        {
            schedule(reason);
        }
    }

    private static string BuildWatchRelativePath(string projectPath, string filePath)
    {
        return Path.GetRelativePath(projectPath, filePath).Replace('\\', '/');
    }

}
