using ReportGraph.Core.Models;

namespace ReportGraph.Adapters.Services;

public interface IReportGraphBuildInputAdapter
{
    Task<ReportGraphBuildInput> LoadAsync(string path, CancellationToken cancellationToken = default);
}
