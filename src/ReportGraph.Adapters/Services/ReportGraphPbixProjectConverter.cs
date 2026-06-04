namespace ReportGraph.Adapters.Services;

public sealed class ReportGraphPbixProjectConverter : IPbixProjectConverter
{
    public ReportGraphPbixProjectConverter()
    {
    }

    public async Task<string> ConvertToPbipProjectAsync(string pbixPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pbixPath);

        var fullPbixPath = Path.GetFullPath(pbixPath);
        if (!File.Exists(fullPbixPath))
        {
            throw new FileNotFoundException($"PBIX file was not found: {fullPbixPath}", fullPbixPath);
        }

        var siblingProject = ResolveSiblingPbipProject(fullPbixPath);
        if (siblingProject is not null)
        {
            return siblingProject;
        }

        throw new NotSupportedException(
            "PBIX is recognized as an input entry, but ReportGraph does not directly analyze PBIX files. " +
            $"Open '{fullPbixPath}' in Power BI Desktop, use 'Save as' to convert it into a Power BI Project (PBIP), " +
            "then run ReportGraph again against the converted project or keep the PBIP beside the PBIX for automatic reuse.");
    }

    private static string? ResolveSiblingPbipProject(string pbixPath)
    {
        var directoryPath = Path.GetDirectoryName(pbixPath)!;
        var baseName = Path.GetFileNameWithoutExtension(pbixPath);
        var sameNamePbip = Path.Combine(directoryPath, $"{baseName}.pbip");
        if (File.Exists(sameNamePbip))
        {
            return directoryPath;
        }

        return ResolvePbipProjectRoot(directoryPath);
    }

    private static string? ResolvePbipProjectRoot(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return null;
        }

        var pbipFiles = Directory.GetFiles(rootDirectory, "*.pbip", SearchOption.TopDirectoryOnly);
        if (pbipFiles.Length == 1)
        {
            return rootDirectory;
        }

        var reportDirectories = Directory.GetDirectories(rootDirectory, "*.Report", SearchOption.TopDirectoryOnly);
        var semanticModelDirectories = Directory.GetDirectories(rootDirectory, "*.SemanticModel", SearchOption.TopDirectoryOnly);
        if (reportDirectories.Length == 1 && semanticModelDirectories.Length == 1)
        {
            return rootDirectory;
        }

        foreach (var childDirectory in Directory.GetDirectories(rootDirectory, "*", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var childProject = ResolvePbipProjectRoot(childDirectory);
            if (childProject is not null)
            {
                return childProject;
            }
        }

        return null;
    }
}
