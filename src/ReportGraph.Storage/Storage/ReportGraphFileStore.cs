using System.Text;
using ReportGraph.Core.Models;
using ReportGraph.Storage.Artifacts;
using ReportGraph.Storage.Serialization;
using GraphManifest = ReportGraph.Core.Models.ReportGraphManifest;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.Storage.Storage;

public sealed class ReportGraphFileStore : IReportGraphFileStore
{
    public ReportGraphArtifactPaths GetPaths(string pbipProjectPath)
    {
        return ReportGraphPathResolver.Resolve(pbipProjectPath);
    }

    public async Task SaveGraphAsync(string pbipProjectPath, GraphModel graph, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var paths = GetPaths(pbipProjectPath);
        EnsureDirectories(paths);
        await WriteFileAsync(paths.ReportGraphFilePath, ReportGraphJson.Serialize(graph), cancellationToken);
    }

    public async Task SaveManifestAsync(string pbipProjectPath, GraphManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var paths = GetPaths(pbipProjectPath);
        EnsureDirectories(paths);
        await WriteFileAsync(paths.ManifestFilePath, ReportGraphJson.Serialize(manifest), cancellationToken);
    }

    public async Task SaveDirtyStateAsync(string pbipProjectPath, ReportGraphDirtyState dirtyState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dirtyState);

        var paths = GetPaths(pbipProjectPath);
        EnsureDirectories(paths);
        await WriteFileAsync(paths.DirtyStateFilePath, ReportGraphJson.Serialize(dirtyState), cancellationToken);
    }

    public async Task<GraphModel?> LoadGraphAsync(string pbipProjectPath, CancellationToken cancellationToken = default)
    {
        var paths = GetPaths(pbipProjectPath);
        if (!File.Exists(paths.ReportGraphFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(paths.ReportGraphFilePath, cancellationToken);
        return ReportGraphJson.Deserialize<GraphModel>(json);
    }

    public async Task<GraphManifest?> LoadManifestAsync(string pbipProjectPath, CancellationToken cancellationToken = default)
    {
        var paths = GetPaths(pbipProjectPath);
        if (!File.Exists(paths.ManifestFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(paths.ManifestFilePath, cancellationToken);
        return ReportGraphJson.Deserialize<GraphManifest>(json);
    }

    public async Task<ReportGraphDirtyState?> LoadDirtyStateAsync(string pbipProjectPath, CancellationToken cancellationToken = default)
    {
        var paths = GetPaths(pbipProjectPath);
        if (!File.Exists(paths.DirtyStateFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(paths.DirtyStateFilePath, cancellationToken);
        return ReportGraphJson.Deserialize<ReportGraphDirtyState>(json);
    }

    public Task DeleteDirtyStateAsync(string pbipProjectPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var paths = GetPaths(pbipProjectPath);
        if (File.Exists(paths.DirtyStateFilePath))
        {
            File.Delete(paths.DirtyStateFilePath);
        }

        return Task.CompletedTask;
    }

    private static void EnsureDirectories(ReportGraphArtifactPaths paths)
    {
        Directory.CreateDirectory(paths.GraphDirectoryPath);
        Directory.CreateDirectory(paths.ContextDirectoryPath);
        Directory.CreateDirectory(paths.PagesDirectoryPath);
    }

    private static async Task WriteFileAsync(string path, string contents, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path, contents + Environment.NewLine, Encoding.UTF8, cancellationToken);
    }
}
