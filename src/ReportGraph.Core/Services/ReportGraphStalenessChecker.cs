using ReportGraph.Core.Models;

namespace ReportGraph.Core.Services;

public interface IReportGraphStalenessChecker
{
    bool IsStale(ReportGraphManifest manifest, string currentModelFingerprint, string currentReportFingerprint);
}

public sealed class ReportGraphStalenessChecker : IReportGraphStalenessChecker
{
    public bool IsStale(ReportGraphManifest manifest, string currentModelFingerprint, string currentReportFingerprint)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentModelFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentReportFingerprint);

        return manifest.IsStale
            || !string.Equals(manifest.ModelFingerprint, currentModelFingerprint, StringComparison.Ordinal)
            || !string.Equals(manifest.ReportFingerprint, currentReportFingerprint, StringComparison.Ordinal);
    }
}
