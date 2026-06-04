namespace ReportGraph.Adapters.Services;

public interface IPbixProjectConverter
{
    Task<string> ConvertToPbipProjectAsync(string pbixPath, CancellationToken cancellationToken = default);
}
