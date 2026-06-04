namespace ReportGraph.Storage.Artifacts;

public static class ReportGraphPathResolver
{
    public static ReportGraphArtifactPaths Resolve(string pbipProjectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pbipProjectPath);

        var projectRoot = Path.GetFullPath(pbipProjectPath);
        var graphDirectoryPath = Path.Combine(projectRoot, "Graph");
        var contextDirectoryPath = Path.Combine(graphDirectoryPath, "context");
        var pagesDirectoryPath = Path.Combine(contextDirectoryPath, "pages");

        return new ReportGraphArtifactPaths(
            GraphDirectoryPath: graphDirectoryPath,
            ContextDirectoryPath: contextDirectoryPath,
            PagesDirectoryPath: pagesDirectoryPath,
            ReportGraphFilePath: Path.Combine(graphDirectoryPath, "report-graph.json"),
            ManifestFilePath: Path.Combine(graphDirectoryPath, "manifest.json"));
    }
}
