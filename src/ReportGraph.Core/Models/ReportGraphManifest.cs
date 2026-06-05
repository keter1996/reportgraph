namespace ReportGraph.Core.Models;

public sealed record ReportGraphManifest(
    string Version,
    string GraphBuilderVersion,
    DateTimeOffset GeneratedAtUtc,
    string PbipProjectPath,
    string ReportRootPath,
    string ModelFingerprint,
    string ReportFingerprint,
    bool IsStale,
    string? SourceFingerprint = null,
    IReadOnlyList<SourceArtifactInput>? SourceFiles = null,
    string? StaleReason = null);
