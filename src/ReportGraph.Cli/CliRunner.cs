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
                "query" or "q" => await RunQueryAsync(remaining, cancellationToken),
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
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FileNotFoundException or DirectoryNotFoundException)
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
        var projectPath = ResolveProjectPath(targetPath);
        var paths = ReportGraphPathResolver.Resolve(projectPath);
        var graph = await graphService.LoadAsync(projectPath, cancellationToken);
        var manifest = await fileStore.LoadManifestAsync(projectPath, cancellationToken);

        var lines = new List<string>
        {
            $"Project root: {projectPath}",
            $"Graph directory: {paths.GraphDirectoryPath}",
            $"Graph exists: {Directory.Exists(paths.GraphDirectoryPath)}",
            $"Graph file exists: {File.Exists(paths.ReportGraphFilePath)}",
            $"Manifest exists: {File.Exists(paths.ManifestFilePath)}"
        };

        if (manifest is not null)
        {
            lines.Add($"Graph version: {manifest.Version}");
            lines.Add($"Generated at: {manifest.GeneratedAtUtc:O}");
            lines.Add($"Model fingerprint: {manifest.ModelFingerprint}");
            lines.Add($"Report fingerprint: {manifest.ReportFingerprint}");
        }

        if (graph is not null)
        {
            lines.Add($"Report name: {graph.Report.ReportName ?? "unknown"}");
            lines.Add($"Page count: {graph.Report.Pages.Count}");
            lines.Add($"Table count: {graph.Model.Tables.Count}");
        }

        return string.Join(Environment.NewLine, lines);
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

        var graph = await graphService.LoadAsync(projectPath, cancellationToken);
        if (graph is null)
        {
            throw new InvalidOperationException(
                $"No graph artifacts were found under '{projectPath}'. Run refresh first.");
        }

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
                string.Equals(Path.GetExtension(fullPath), ".pbip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(fullPath), ".pbix", StringComparison.OrdinalIgnoreCase)))
        {
            return Path.GetDirectoryName(fullPath)!;
        }

        return fullPath;
    }

}
