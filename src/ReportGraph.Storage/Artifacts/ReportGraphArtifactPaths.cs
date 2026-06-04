namespace ReportGraph.Storage.Artifacts;

public sealed record ReportGraphArtifactPaths(
    string GraphDirectoryPath,
    string ContextDirectoryPath,
    string PagesDirectoryPath,
    string ReportGraphFilePath,
    string ManifestFilePath);
