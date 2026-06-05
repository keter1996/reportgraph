using ReportGraph.Core.Models;

namespace ReportGraph.Core.Services;

public interface IReportGraphStalenessChecker
{
    ReportGraphStalenessResult Evaluate(
        ReportGraphManifest manifest,
        string? currentSourceFingerprint,
        string currentModelFingerprint,
        string currentReportFingerprint);
}

public sealed class ReportGraphStalenessChecker : IReportGraphStalenessChecker
{
    public ReportGraphStalenessResult Evaluate(
        ReportGraphManifest manifest,
        string? currentSourceFingerprint,
        string currentModelFingerprint,
        string currentReportFingerprint)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentModelFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentReportFingerprint);

        if (manifest.IsStale)
        {
            return new ReportGraphStalenessResult(true, manifest.StaleReason ?? "Manifest marked stale");
        }

        if (!string.IsNullOrWhiteSpace(currentSourceFingerprint))
        {
            if (string.IsNullOrWhiteSpace(manifest.SourceFingerprint))
            {
                return new ReportGraphStalenessResult(true, "Manifest source fingerprint missing");
            }

            if (!string.Equals(manifest.SourceFingerprint, currentSourceFingerprint, StringComparison.Ordinal))
            {
                return new ReportGraphStalenessResult(true, "Source fingerprint changed");
            }
        }

        if (!string.Equals(manifest.ModelFingerprint, currentModelFingerprint, StringComparison.Ordinal))
        {
            return new ReportGraphStalenessResult(true, "Model fingerprint changed");
        }

        if (!string.Equals(manifest.ReportFingerprint, currentReportFingerprint, StringComparison.Ordinal))
        {
            return new ReportGraphStalenessResult(true, "Report fingerprint changed");
        }

        return new ReportGraphStalenessResult(false, null);
    }
}

public sealed record ReportGraphStalenessResult(
    bool IsStale,
    string? Reason);
