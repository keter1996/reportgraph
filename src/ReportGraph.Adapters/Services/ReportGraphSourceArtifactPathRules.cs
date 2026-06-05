namespace ReportGraph.Adapters.Services;

public static class ReportGraphSourceArtifactPathRules
{
    public static bool IsTrackedSourceFile(string projectRootPath, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullProjectRoot = Path.GetFullPath(projectRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullFilePath = Path.GetFullPath(filePath);

        if (!fullFilePath.StartsWith(fullProjectRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(fullProjectRoot, fullFilePath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.StartsWith(".", StringComparison.Ordinal) ||
                segment.Equals("Graph", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals(".pbi", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var fileName = Path.GetFileName(fullFilePath);
        if (fileName.StartsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(fileName, "report-graph.build-input.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(Path.GetExtension(fileName), ".pbip", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(Path.GetExtension(fileName), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var topSegment = segments[0];
        return topSegment.EndsWith(".Report", StringComparison.OrdinalIgnoreCase) ||
               topSegment.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase);
    }
}
