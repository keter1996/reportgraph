using ReportGraph.Core.Services;

namespace ReportGraph.Storage.Storage;

public interface IReportGraphContextFileStore
{
    Task SaveContextAsync(string pbipProjectPath, RenderedReportGraphContext context, CancellationToken cancellationToken = default);
}
