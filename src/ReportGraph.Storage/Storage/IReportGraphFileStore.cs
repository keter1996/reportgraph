using ReportGraph.Core.Models;
using ReportGraph.Storage.Artifacts;
using GraphManifest = ReportGraph.Core.Models.ReportGraphManifest;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.Storage.Storage;

public interface IReportGraphFileStore
{
    ReportGraphArtifactPaths GetPaths(string pbipProjectPath);
    Task SaveGraphAsync(string pbipProjectPath, GraphModel graph, CancellationToken cancellationToken = default);
    Task SaveManifestAsync(string pbipProjectPath, GraphManifest manifest, CancellationToken cancellationToken = default);
    Task SaveDirtyStateAsync(string pbipProjectPath, ReportGraphDirtyState dirtyState, CancellationToken cancellationToken = default);
    Task<GraphModel?> LoadGraphAsync(string pbipProjectPath, CancellationToken cancellationToken = default);
    Task<GraphManifest?> LoadManifestAsync(string pbipProjectPath, CancellationToken cancellationToken = default);
    Task<ReportGraphDirtyState?> LoadDirtyStateAsync(string pbipProjectPath, CancellationToken cancellationToken = default);
    Task DeleteDirtyStateAsync(string pbipProjectPath, CancellationToken cancellationToken = default);
}
