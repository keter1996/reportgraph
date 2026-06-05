using ReportGraph.Core.Models;
using ReportGraph.Storage.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReportGraph.Adapters.Services;

public sealed class ReportGraphProjectAdapter : IReportGraphBuildInputAdapter
{
    private static readonly string[] CandidateBuildInputRelativePaths =
    [
        "report-graph.build-input.json",
        Path.Combine("Graph", "source", "report-graph.build-input.json"),
        Path.Combine("Graph", "report-graph.build-input.json")
    ];

    public ReportGraphProjectAdapter()
    {
    }

    public async Task<ReportGraphBuildInput> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (IsPbixPath(fullPath))
        {
            throw new NotSupportedException(
                $"PBIX is not a supported input for ReportGraph. Please open '{fullPath}' in Power BI Desktop and save it as a PBIP project before running ReportGraph.");
        }

        var inputFilePath = ResolveBuildInputPath(fullPath);
        if (inputFilePath is null)
        {
            return await LoadFromPbipProjectAsync(fullPath, cancellationToken);
        }

        var json = await File.ReadAllTextAsync(inputFilePath, cancellationToken);
        var input = ReportGraphJson.Deserialize<ReportGraphBuildInput>(json);
        if (input is null)
        {
            throw new InvalidOperationException($"Could not deserialize build input: {inputFilePath}");
        }

        return input;
    }

    private static async Task<ReportGraphBuildInput> LoadFromPbipProjectAsync(string fullPath, CancellationToken cancellationToken)
    {
        var projectContext = await ResolveProjectContextAsync(fullPath, cancellationToken);
        var model = await LoadSemanticModelAsync(projectContext.SemanticModelDirectoryPath, cancellationToken);
        var report = await LoadReportAsync(projectContext.ReportDirectoryPath, model, cancellationToken);
        var documents = await LoadMarkdownDocumentsAsync(projectContext.ProjectRootPath, cancellationToken);
        var sourceFiles = await LoadSourceFilesAsync(projectContext, cancellationToken);
        var generatedAtUtc = ResolveGeneratedAtUtc(sourceFiles, report.PagesLastModifiedUtc);

        return new ReportGraphBuildInput(
            Version: "1.0",
            GeneratedAtUtc: generatedAtUtc,
            Source: new ReportGraphSource(
                InstanceId: projectContext.ProjectName,
                PbipProjectPath: projectContext.ProjectRootPath,
                ReportRootPath: projectContext.ReportDirectoryPath,
                ModelName: model.ModelName),
            Report: report,
            Model: model,
            SourceFiles: sourceFiles,
            Documents: documents);
    }

    private static string? ResolveBuildInputPath(string fullPath)
    {
        if (Directory.Exists(fullPath))
        {
            return ResolveFromDirectory(fullPath);
        }

        if (File.Exists(fullPath))
        {
            if (string.Equals(Path.GetExtension(fullPath), ".pbip", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveFromDirectory(Path.GetDirectoryName(fullPath)!);
            }

            return fullPath;
        }

        return null;
    }

    private static bool IsPbixPath(string fullPath) =>
        File.Exists(fullPath) &&
        string.Equals(Path.GetExtension(fullPath), ".pbix", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ResolveGeneratedAtUtc(
        IReadOnlyList<SourceArtifactInput> sourceFiles,
        DateTimeOffset reportPagesLastModifiedUtc)
    {
        var candidateTimes = new List<DateTimeOffset> { reportPagesLastModifiedUtc };
        candidateTimes.AddRange(sourceFiles.Select(file => file.LastModifiedUtc));

        return candidateTimes.Count == 0
            ? DateTimeOffset.UtcNow
            : candidateTimes.Max();
    }

    private static async Task<IReadOnlyList<MarkdownDocumentInput>> LoadMarkdownDocumentsAsync(
        string projectRootPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(projectRootPath))
        {
            return [];
        }

        var documents = new List<MarkdownDocumentInput>();
        foreach (var filePath in Directory.EnumerateFiles(projectRootPath, "*.md", SearchOption.AllDirectories)
                     .Where(path => IsMarkdownSourceDocument(projectRootPath, path))
                     .OrderBy(path => Path.GetRelativePath(projectRootPath, path), StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(projectRootPath, filePath).Replace('\\', '/');
            documents.Add(new MarkdownDocumentInput(
                Path: relativePath,
                Content: await File.ReadAllTextAsync(filePath, cancellationToken),
                LastModifiedUtc: File.GetLastWriteTimeUtc(filePath)));
        }

        return documents;
    }

    private static bool IsMarkdownSourceDocument(string projectRootPath, string filePath)
    {
        return ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectRootPath, filePath) &&
               string.Equals(Path.GetExtension(filePath), ".md", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<SourceArtifactInput>> LoadSourceFilesAsync(
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        var orderedPaths = Directory.EnumerateFiles(projectContext.ProjectRootPath, "*", SearchOption.AllDirectories)
            .Where(path => ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectContext.ProjectRootPath, path))
            .OrderBy(path => Path.GetRelativePath(projectContext.ProjectRootPath, path).Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();

        var sourceFiles = new List<SourceArtifactInput>(orderedPaths.Length);
        foreach (var filePath in orderedPaths)
        {
            sourceFiles.Add(new SourceArtifactInput(
                Path: Path.GetRelativePath(projectContext.ProjectRootPath, filePath).Replace('\\', '/'),
                ContentHash: await ComputeFileHashAsync(filePath, cancellationToken),
                LastModifiedUtc: File.GetLastWriteTimeUtc(filePath)));
        }

        return sourceFiles;
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return $"sha256:{Convert.ToHexStringLower(hash)}";
    }

    private static async Task<ProjectContext> ResolveProjectContextAsync(string fullPath, CancellationToken cancellationToken)
    {
        var projectRootPath = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath)!;

        var pbipFilePath = File.Exists(fullPath) && string.Equals(Path.GetExtension(fullPath), ".pbip", StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : Directory.GetFiles(projectRootPath, "*.pbip", SearchOption.TopDirectoryOnly).SingleOrDefault();

        string reportDirectoryPath;
        if (pbipFilePath is not null)
        {
            reportDirectoryPath = await ResolveReportDirectoryFromPbipAsync(pbipFilePath, cancellationToken);
        }
        else
        {
            var reportDirectories = Directory.GetDirectories(projectRootPath, "*.Report", SearchOption.TopDirectoryOnly);
            reportDirectoryPath = reportDirectories.Length switch
            {
                1 => reportDirectories[0],
                0 => throw new DirectoryNotFoundException(
                    $"Could not locate a report folder under '{projectRootPath}'. Expected a '*.Report' directory or a .pbip file."),
                _ => throw new InvalidOperationException(
                    $"Multiple report folders were found under '{projectRootPath}'. Provide a .pbip file to disambiguate.")
            };
        }

        var semanticModelDirectoryPath = await ResolveSemanticModelDirectoryAsync(projectRootPath, reportDirectoryPath, cancellationToken);
        return new ProjectContext(
            PbipFilePath: pbipFilePath,
            ProjectRootPath: projectRootPath,
            ProjectName: Path.GetFileNameWithoutExtension(pbipFilePath ?? projectRootPath.TrimEnd(Path.DirectorySeparatorChar)),
            ReportDirectoryPath: reportDirectoryPath,
            SemanticModelDirectoryPath: semanticModelDirectoryPath);
    }

    private static async Task<string> ResolveReportDirectoryFromPbipAsync(string pbipFilePath, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(pbipFilePath, cancellationToken));
        var artifacts = document.RootElement.GetProperty("artifacts");
        if (artifacts.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"No report artifacts were found in '{pbipFilePath}'.");
        }

        var reportPath = artifacts[0].GetProperty("report").GetProperty("path").GetString();
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            throw new InvalidOperationException($"The report path is missing in '{pbipFilePath}'.");
        }

        var pbipDirectory = Path.GetDirectoryName(pbipFilePath)!;
        return Path.GetFullPath(Path.Combine(pbipDirectory, reportPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static async Task<string> ResolveSemanticModelDirectoryAsync(
        string projectRootPath,
        string reportDirectoryPath,
        CancellationToken cancellationToken)
    {
        var definitionPbirPath = Path.Combine(reportDirectoryPath, "definition.pbir");
        if (File.Exists(definitionPbirPath))
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(definitionPbirPath, cancellationToken));
            if (document.RootElement.TryGetProperty("datasetReference", out var datasetReference) &&
                datasetReference.TryGetProperty("byPath", out var byPath) &&
                byPath.TryGetProperty("path", out var pathElement))
            {
                var relativePath = pathElement.GetString();
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    return Path.GetFullPath(Path.Combine(reportDirectoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
        }

        var semanticModelDirectories = Directory.GetDirectories(projectRootPath, "*.SemanticModel", SearchOption.TopDirectoryOnly);
        return semanticModelDirectories.Length switch
        {
            1 => semanticModelDirectories[0],
            0 => throw new DirectoryNotFoundException(
                $"Could not locate a semantic model folder under '{projectRootPath}'. Expected a '*.SemanticModel' directory."),
            _ => throw new InvalidOperationException(
                $"Multiple semantic model folders were found under '{projectRootPath}'. The report definition must specify datasetReference.byPath.")
        };
    }

    private static async Task<SemanticModelInput> LoadSemanticModelAsync(string semanticModelDirectoryPath, CancellationToken cancellationToken)
    {
        var modelBimPath = Path.Combine(semanticModelDirectoryPath, "model.bim");
        if (File.Exists(modelBimPath))
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(modelBimPath, cancellationToken));
            var root = document.RootElement;
            var modelNode = root.TryGetProperty("model", out var nestedModel) ? nestedModel : root;
            var modelName = root.TryGetProperty("name", out var rootName)
                ? rootName.GetString()
                : modelNode.TryGetProperty("name", out var modelNameNode)
                    ? modelNameNode.GetString()
                    : Path.GetFileNameWithoutExtension(semanticModelDirectoryPath);

            var tables = new List<TableInput>();
            var columns = new List<ColumnInput>();
            var measures = new List<MeasureInput>();
            if (modelNode.TryGetProperty("tables", out var tablesNode))
            {
                foreach (var tableNode in tablesNode.EnumerateArray())
                {
                    var tableName = tableNode.GetProperty("name").GetString() ?? "UnknownTable";
                    var isHidden = tableNode.TryGetProperty("isHidden", out var hiddenNode) && hiddenNode.GetBoolean();
                    var columnNames = tableNode.TryGetProperty("columns", out var columnsNode)
                        ? columnsNode.EnumerateArray()
                            .Select(column =>
                            {
                                var name = column.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    columns.Add(new ColumnInput(
                                        Table: tableName,
                                        Name: name!,
                                        DisplayFolder: column.TryGetProperty("displayFolder", out var displayFolderNode) ? displayFolderNode.GetString() : null,
                                        FormatString: column.TryGetProperty("formatString", out var formatStringNode) ? formatStringNode.GetString() : null));
                                }

                                return name;
                            })
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Cast<string>()
                            .ToArray()
                        : [];
                    var measureNames = tableNode.TryGetProperty("measures", out var measuresNode)
                        ? measuresNode.EnumerateArray()
                            .Select(measure =>
                            {
                                var name = measure.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    measures.Add(new MeasureInput(
                                        Table: tableName,
                                        Name: name!,
                                        DisplayFolder: measure.TryGetProperty("displayFolder", out var displayFolderNode) ? displayFolderNode.GetString() : null,
                                        FormatString: measure.TryGetProperty("formatString", out var formatStringNode) ? formatStringNode.GetString() : null,
                                        Expression: measure.TryGetProperty("expression", out var expressionNode) ? expressionNode.GetString() : null));
                                }

                                return name;
                            })
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Cast<string>()
                            .ToArray()
                        : [];

                    tables.Add(new TableInput(tableName, isHidden, columnNames, measureNames));
                }
            }

            var relationships = new List<RelationshipInput>();
            if (modelNode.TryGetProperty("relationships", out var relationshipsNode))
            {
                foreach (var relationshipNode in relationshipsNode.EnumerateArray())
                {
                    var fromTable = relationshipNode.TryGetProperty("fromTable", out var fromTableNode) ? fromTableNode.GetString() ?? string.Empty : string.Empty;
                    var fromColumn = relationshipNode.TryGetProperty("fromColumn", out var fromColumnNode) ? fromColumnNode.GetString() ?? string.Empty : string.Empty;
                    var toTable = relationshipNode.TryGetProperty("toTable", out var toTableNode) ? toTableNode.GetString() ?? string.Empty : string.Empty;
                    var toColumn = relationshipNode.TryGetProperty("toColumn", out var toColumnNode) ? toColumnNode.GetString() ?? string.Empty : string.Empty;
                    var relationshipId = relationshipNode.TryGetProperty("name", out var nameNode) && !string.IsNullOrWhiteSpace(nameNode.GetString())
                        ? nameNode.GetString()!
                        : $"{fromTable}.{fromColumn}->{toTable}.{toColumn}";
                    var isActive = !relationshipNode.TryGetProperty("isActive", out var activeNode) || activeNode.GetBoolean();

                    relationships.Add(new RelationshipInput(relationshipId, fromTable, fromColumn, toTable, toColumn, isActive));
                }
            }

            return new SemanticModelInput(modelName, tables, relationships, columns, measures);
        }

        var definitionDirectoryPath = Path.Combine(semanticModelDirectoryPath, "definition");
        if (Directory.Exists(definitionDirectoryPath))
        {
            return await LoadTmdlSemanticModelAsync(semanticModelDirectoryPath, definitionDirectoryPath, cancellationToken);
        }

        throw new NotSupportedException(
            $"Semantic model folder '{semanticModelDirectoryPath}' does not contain model.bim or a definition directory. Unsupported semantic model format.");
    }

    private static async Task<SemanticModelInput> LoadTmdlSemanticModelAsync(
        string semanticModelDirectoryPath,
        string definitionDirectoryPath,
        CancellationToken cancellationToken)
    {
        var modelName = await ResolveTmdlModelNameAsync(definitionDirectoryPath, semanticModelDirectoryPath, cancellationToken);
        var tablesDirectoryPath = Path.Combine(definitionDirectoryPath, "tables");
        var tableResult = Directory.Exists(tablesDirectoryPath)
            ? await LoadTmdlTablesAsync(tablesDirectoryPath, cancellationToken)
            : new TmdlTableLoadResult([], [], []);
        var relationshipsPath = Path.Combine(definitionDirectoryPath, "relationships.tmdl");
        var relationships = File.Exists(relationshipsPath)
            ? await LoadTmdlRelationshipsAsync(relationshipsPath, cancellationToken)
            : [];

        return new SemanticModelInput(modelName, tableResult.Tables, relationships, tableResult.Columns, tableResult.Measures);
    }

    private static async Task<string?> ResolveTmdlModelNameAsync(
        string definitionDirectoryPath,
        string semanticModelDirectoryPath,
        CancellationToken cancellationToken)
    {
        foreach (var fileName in new[] { "database.tmdl", "model.tmdl" })
        {
            var filePath = Path.Combine(definitionDirectoryPath, fileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("database ", StringComparison.OrdinalIgnoreCase))
                {
                    return UnquoteTmdlName(trimmed["database ".Length..]);
                }

                if (trimmed.StartsWith("model ", StringComparison.OrdinalIgnoreCase))
                {
                    return UnquoteTmdlName(trimmed["model ".Length..]);
                }
            }
        }

        return Path.GetFileNameWithoutExtension(semanticModelDirectoryPath);
    }

    private static async Task<TmdlTableLoadResult> LoadTmdlTablesAsync(string tablesDirectoryPath, CancellationToken cancellationToken)
    {
        var tables = new List<TableInput>();
        var columns = new List<ColumnInput>();
        var measures = new List<MeasureInput>();
        foreach (var filePath in Directory.GetFiles(tablesDirectoryPath, "*.tmdl", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            string? tableName = null;
            var isHidden = false;
            var columnNames = new List<string>();
            var measureNames = new List<string>();
            string? memberKind = null;
            string? memberName = null;
            string? memberDisplayFolder = null;
            string? memberFormatString = null;
            string? memberExpression = null;

            void FlushMember()
            {
                if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(memberKind) || string.IsNullOrWhiteSpace(memberName))
                {
                    memberKind = null;
                    memberName = null;
                    memberDisplayFolder = null;
                    memberFormatString = null;
                    memberExpression = null;
                    return;
                }

                if (string.Equals(memberKind, "column", StringComparison.OrdinalIgnoreCase))
                {
                    columnNames.Add(memberName);
                    columns.Add(new ColumnInput(tableName, memberName, memberDisplayFolder, memberFormatString));
                }
                else if (string.Equals(memberKind, "measure", StringComparison.OrdinalIgnoreCase))
                {
                    measureNames.Add(memberName);
                    measures.Add(new MeasureInput(tableName, memberName, memberDisplayFolder, memberFormatString, memberExpression));
                }

                memberKind = null;
                memberName = null;
                memberDisplayFolder = null;
                memberFormatString = null;
                memberExpression = null;
            }

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("table ", StringComparison.OrdinalIgnoreCase))
                {
                    FlushMember();
                    tableName = ParseTmdlDeclarationName(trimmed["table ".Length..]);
                    continue;
                }

                if (trimmed.Equals("isHidden", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("isHidden: true", StringComparison.OrdinalIgnoreCase))
                {
                    isHidden = true;
                    continue;
                }

                if (trimmed.StartsWith("column ", StringComparison.OrdinalIgnoreCase))
                {
                    FlushMember();
                    memberKind = "column";
                    memberName = ParseTmdlDeclarationName(trimmed["column ".Length..]);
                    memberExpression = ParseInlineDeclarationExpression(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("measure ", StringComparison.OrdinalIgnoreCase))
                {
                    FlushMember();
                    memberKind = "measure";
                    memberName = ParseTmdlDeclarationName(trimmed["measure ".Length..]);
                    memberExpression = ParseInlineDeclarationExpression(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("displayFolder:", StringComparison.OrdinalIgnoreCase))
                {
                    memberDisplayFolder = trimmed["displayFolder:".Length..].Trim();
                    continue;
                }

                if (trimmed.StartsWith("formatString:", StringComparison.OrdinalIgnoreCase))
                {
                    memberFormatString = trimmed["formatString:".Length..].Trim();
                }
            }

            FlushMember();

            if (!string.IsNullOrWhiteSpace(tableName))
            {
                tables.Add(new TableInput(tableName, isHidden, columnNames, measureNames));
            }
        }

        return new TmdlTableLoadResult(tables, columns, measures);
    }

    private static async Task<IReadOnlyList<RelationshipInput>> LoadTmdlRelationshipsAsync(string relationshipsPath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(relationshipsPath, cancellationToken);
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var relationships = new List<RelationshipInput>();

        string? relationshipId = null;
        string? fromTable = null;
        string? fromColumn = null;
        string? toTable = null;
        string? toColumn = null;
        var isActive = true;

        void Flush()
        {
            if (!string.IsNullOrWhiteSpace(relationshipId) &&
                !string.IsNullOrWhiteSpace(fromTable) &&
                !string.IsNullOrWhiteSpace(fromColumn) &&
                !string.IsNullOrWhiteSpace(toTable) &&
                !string.IsNullOrWhiteSpace(toColumn))
            {
                relationships.Add(new RelationshipInput(
                    relationshipId,
                    fromTable,
                    fromColumn,
                    toTable,
                    toColumn,
                    isActive));
            }
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("relationship ", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                relationshipId = ParseTmdlDeclarationName(trimmed["relationship ".Length..]);
                fromTable = null;
                fromColumn = null;
                toTable = null;
                toColumn = null;
                isActive = true;
                continue;
            }

            if (trimmed.StartsWith("fromColumn:", StringComparison.OrdinalIgnoreCase))
            {
                var reference = ParseTmdlObjectReference(trimmed["fromColumn:".Length..].Trim());
                fromTable = reference.Table;
                fromColumn = reference.Field;
                continue;
            }

            if (trimmed.StartsWith("toColumn:", StringComparison.OrdinalIgnoreCase))
            {
                var reference = ParseTmdlObjectReference(trimmed["toColumn:".Length..].Trim());
                toTable = reference.Table;
                toColumn = reference.Field;
                continue;
            }

            if (trimmed.StartsWith("isActive:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["isActive:".Length..].Trim();
                isActive = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
            }
        }

        Flush();
        return relationships;
    }

    private static (string Table, string Field) ParseTmdlObjectReference(string reference)
    {
        var match = Regex.Match(reference, @"^'?(?<table>[^'\[]+)'?\[(?<field>[^\]]+)\]$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return (
                match.Groups["table"].Value.Trim(),
                match.Groups["field"].Value.Trim());
        }

        var normalized = reference.Replace("'", string.Empty);
        var lastDotIndex = normalized.LastIndexOf('.');
        if (lastDotIndex > 0 && lastDotIndex < normalized.Length - 1)
        {
            return (
                normalized[..lastDotIndex].Trim(),
                normalized[(lastDotIndex + 1)..].Trim());
        }

        return ("Unknown", normalized.Trim());
    }

    private static string UnquoteTmdlName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("'") && trimmed.EndsWith("'") && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static string ParseTmdlDeclarationName(string value)
    {
        var trimmed = value.Trim();
        var delimiterIndex = FindFirstDeclarationDelimiter(trimmed);
        var candidate = delimiterIndex >= 0 ? trimmed[..delimiterIndex] : trimmed;
        return UnquoteTmdlName(candidate.Trim());
    }

    private static string? ParseInlineDeclarationExpression(string declarationLine)
    {
        var equalsIndex = declarationLine.IndexOf('=');
        if (equalsIndex < 0 || equalsIndex >= declarationLine.Length - 1)
        {
            return null;
        }

        return declarationLine[(equalsIndex + 1)..].Trim();
    }

    private static int FindFirstDeclarationDelimiter(string value)
    {
        var equalsIndex = value.IndexOf('=');
        var colonIndex = value.IndexOf(':');

        return (equalsIndex, colonIndex) switch
        {
            (< 0, < 0) => -1,
            (>= 0, < 0) => equalsIndex,
            (< 0, >= 0) => colonIndex,
            _ => Math.Min(equalsIndex, colonIndex)
        };
    }

    private static async Task<ReportInput> LoadReportAsync(
        string reportDirectoryPath,
        SemanticModelInput model,
        CancellationToken cancellationToken)
    {
        var definitionDirectoryPath = Path.Combine(reportDirectoryPath, "definition");
        if (!Directory.Exists(definitionDirectoryPath))
        {
            throw new NotSupportedException(
                $"Report folder '{reportDirectoryPath}' does not contain a PBIR definition folder. PBIR-Legacy report.json parsing is not implemented yet.");
        }

        var pagesDirectoryPath = Path.Combine(definitionDirectoryPath, "pages");
        if (!Directory.Exists(pagesDirectoryPath))
        {
            throw new DirectoryNotFoundException($"The report definition folder '{definitionDirectoryPath}' does not contain pages.");
        }

        var reportName = Path.GetFileNameWithoutExtension(reportDirectoryPath);
        var pagesMetadataPath = Path.Combine(pagesDirectoryPath, "pages.json");
        string? activePageId = null;
        var pageOrder = Array.Empty<string>();

        if (File.Exists(pagesMetadataPath))
        {
            using var pagesMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(pagesMetadataPath, cancellationToken));
            if (pagesMetadata.RootElement.TryGetProperty("activePageName", out var activePageNameNode))
            {
                activePageId = activePageNameNode.GetString();
            }

            if (pagesMetadata.RootElement.TryGetProperty("pageOrder", out var pageOrderNode))
            {
                pageOrder = pageOrderNode.EnumerateArray()
                    .Select(item => item.GetString())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToArray();
            }
        }

        var pageDirectories = Directory.GetDirectories(pagesDirectoryPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pages = new List<ReportPageInput>();
        var pageLastWriteUtc = new List<DateTimeOffset>();

        foreach (var pageDirectoryPath in pageDirectories)
        {
            var pageJsonPath = Path.Combine(pageDirectoryPath, "page.json");
            if (!File.Exists(pageJsonPath))
            {
                continue;
            }

            using var pageDocument = JsonDocument.Parse(await File.ReadAllTextAsync(pageJsonPath, cancellationToken));
            var pageRoot = pageDocument.RootElement;
            var pageId = pageRoot.TryGetProperty("name", out var pageNameNode)
                ? pageNameNode.GetString() ?? Path.GetFileName(pageDirectoryPath)
                : Path.GetFileName(pageDirectoryPath);
            var displayName = pageRoot.TryGetProperty("displayName", out var displayNameNode)
                ? displayNameNode.GetString() ?? pageId
                : pageId;

            var visualsDirectoryPath = Path.Combine(pageDirectoryPath, "visuals");
            var visuals = new List<VisualInput>();
            if (Directory.Exists(visualsDirectoryPath))
            {
                foreach (var visualDirectoryPath in Directory.GetDirectories(visualsDirectoryPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    var visualJsonPath = Path.Combine(visualDirectoryPath, "visual.json");
                    if (!File.Exists(visualJsonPath))
                    {
                        continue;
                    }

                    using var visualDocument = JsonDocument.Parse(await File.ReadAllTextAsync(visualJsonPath, cancellationToken));
                    var visualRoot = visualDocument.RootElement;
                    var visualId = visualRoot.TryGetProperty("name", out var visualNameNode)
                        ? visualNameNode.GetString() ?? Path.GetFileName(visualDirectoryPath)
                        : Path.GetFileName(visualDirectoryPath);
                    var visualType = visualRoot.TryGetProperty("visual", out var visualConfigNode) &&
                        visualConfigNode.TryGetProperty("visualType", out var visualTypeNode)
                        ? visualTypeNode.GetString() ?? "unknown"
                        : "unknown";

                    var fields = ExtractVisualFields(visualRoot, model);
                    var filters = ExtractVisualFilters(visualRoot);
                    visuals.Add(new VisualInput(visualId, visualType, fields, filters));
                    pageLastWriteUtc.Add(File.GetLastWriteTimeUtc(visualJsonPath));
                }
            }

            pages.Add(new ReportPageInput(pageId, displayName, 0, visuals));
            pageLastWriteUtc.Add(File.GetLastWriteTimeUtc(pageJsonPath));
        }

        var orderedPages = OrderPages(pages, pageOrder);
        var pagesLastModifiedUtc = pageLastWriteUtc.Count == 0
            ? DateTimeOffset.UtcNow
            : pageLastWriteUtc.Max();

        return new ReportInput(reportName, activePageId, pagesLastModifiedUtc, orderedPages);
    }

    private static IReadOnlyList<ReportPageInput> OrderPages(
        IReadOnlyList<ReportPageInput> pages,
        IReadOnlyList<string> pageOrder)
    {
        var ordinalByPage = pageOrder
            .Select((pageId, index) => new { pageId, index })
            .ToDictionary(item => item.pageId, item => item.index, StringComparer.OrdinalIgnoreCase);

        return pages
            .OrderBy(page => ordinalByPage.TryGetValue(page.PageId, out var ordinal) ? ordinal : int.MaxValue)
            .ThenBy(page => page.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select((page, index) => page with { Ordinal = index })
            .ToArray();
    }

    private static IReadOnlyList<VisualFieldInput> ExtractVisualFields(JsonElement visualRoot, SemanticModelInput model)
    {
        if (!visualRoot.TryGetProperty("visual", out var visualNode) ||
            !visualNode.TryGetProperty("query", out var queryNode) ||
            !queryNode.TryGetProperty("queryState", out var queryStateNode))
        {
            return [];
        }

        var fields = new List<VisualFieldInput>();
        foreach (var roleProperty in queryStateNode.EnumerateObject())
        {
            if (!roleProperty.Value.TryGetProperty("projections", out var projectionsNode))
            {
                continue;
            }

            foreach (var projectionNode in projectionsNode.EnumerateArray())
            {
                if (!projectionNode.TryGetProperty("queryRef", out var queryRefNode))
                {
                    continue;
                }

                var queryRef = queryRefNode.GetString();
                if (string.IsNullOrWhiteSpace(queryRef))
                {
                    continue;
                }

                var resolved = ResolveFieldReference(queryRef, model);
                fields.Add(new VisualFieldInput(roleProperty.Name, resolved.Table, resolved.Field, resolved.Kind));
            }
        }

        return fields;
    }

    private static IReadOnlyList<VisualFilterInput> ExtractVisualFilters(JsonElement visualRoot)
    {
        if (!visualRoot.TryGetProperty("visual", out var visualNode) ||
            !visualNode.TryGetProperty("objects", out var objectsNode) ||
            !objectsNode.TryGetProperty("general", out var generalNode))
        {
            return [];
        }

        var filters = new List<VisualFilterInput>();
        foreach (var generalItem in generalNode.EnumerateArray())
        {
            if (!generalItem.TryGetProperty("properties", out var propertiesNode) ||
                !propertiesNode.TryGetProperty("filter", out var filterNode) ||
                !filterNode.TryGetProperty("filter", out var filterDefinitionNode))
            {
                continue;
            }

            filters.AddRange(ParseFilterDefinition(filterDefinitionNode));
        }

        return filters;
    }

    private static IReadOnlyList<VisualFilterInput> ParseFilterDefinition(JsonElement filterDefinitionNode)
    {
        if (!filterDefinitionNode.TryGetProperty("Where", out var whereNode))
        {
            return [];
        }

        var sourceMap = BuildFilterSourceMap(filterDefinitionNode);

        var filters = new List<VisualFilterInput>();
        foreach (var clause in whereNode.EnumerateArray())
        {
            if (!clause.TryGetProperty("Condition", out var conditionNode))
            {
                continue;
            }

            if (conditionNode.TryGetProperty("In", out var inNode))
            {
                var filter = ParseInCondition(inNode, sourceMap);
                if (filter is not null)
                {
                    filters.Add(filter);
                }
            }
        }

        return filters;
    }

    private static Dictionary<string, string> BuildFilterSourceMap(JsonElement filterDefinitionNode)
    {
        var sourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!filterDefinitionNode.TryGetProperty("From", out var fromNode))
        {
            return sourceMap;
        }

        foreach (var source in fromNode.EnumerateArray())
        {
            if (!source.TryGetProperty("Name", out var nameNode) ||
                !source.TryGetProperty("Entity", out var entityNode))
            {
                continue;
            }

            var alias = nameNode.GetString();
            var entity = entityNode.GetString();
            if (!string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(entity))
            {
                sourceMap[alias!] = entity!;
            }
        }

        return sourceMap;
    }

    private static VisualFilterInput? ParseInCondition(JsonElement inNode, IReadOnlyDictionary<string, string> sourceMap)
    {
        if (!inNode.TryGetProperty("Expressions", out var expressionsNode) ||
            expressionsNode.GetArrayLength() == 0)
        {
            return null;
        }

        var firstExpression = expressionsNode[0];
        if (!TryParseColumnExpression(firstExpression, sourceMap, out var table, out var field))
        {
            return null;
        }

        var values = new List<string>();
        if (inNode.TryGetProperty("Values", out var valuesNode))
        {
            foreach (var valueSet in valuesNode.EnumerateArray())
            {
                foreach (var valueNode in valueSet.EnumerateArray())
                {
                    var parsedValue = ParseFilterValue(valueNode);
                    if (!string.IsNullOrWhiteSpace(parsedValue))
                    {
                        values.Add(parsedValue);
                    }
                }
            }
        }

        return new VisualFilterInput(table, field, values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool TryParseColumnExpression(
        JsonElement expressionNode,
        IReadOnlyDictionary<string, string> sourceMap,
        out string table,
        out string field)
    {
        table = string.Empty;
        field = string.Empty;

        if (!expressionNode.TryGetProperty("Column", out var columnNode) ||
            !columnNode.TryGetProperty("Property", out var propertyNode))
        {
            return false;
        }

        field = propertyNode.GetString() ?? string.Empty;
        if (columnNode.TryGetProperty("Expression", out var nestedExpressionNode))
        {
            if (nestedExpressionNode.TryGetProperty("SourceRef", out var sourceRefNode))
            {
                if (sourceRefNode.TryGetProperty("Entity", out var entityNode))
                {
                    table = entityNode.GetString() ?? string.Empty;
                }
                else if (sourceRefNode.TryGetProperty("Source", out var sourceNode))
                {
                    var alias = sourceNode.GetString();
                    if (!string.IsNullOrWhiteSpace(alias) && sourceMap.TryGetValue(alias!, out var entity))
                    {
                        table = entity;
                    }
                }
            }
        }

        return table.Length > 0 && field.Length > 0;
    }

    private static string? ParseFilterValue(JsonElement valueNode)
    {
        if (valueNode.TryGetProperty("Literal", out var literalNode) &&
            literalNode.TryGetProperty("Value", out var literalValueNode))
        {
            return NormalizeLiteralValue(literalValueNode.GetString());
        }

        return null;
    }

    private static string? NormalizeLiteralValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var value = rawValue.Trim();
        if (value.StartsWith("'") && value.EndsWith("'") && value.Length >= 2)
        {
            value = value[1..^1];
        }
        else if (value.EndsWith("L", StringComparison.OrdinalIgnoreCase) && long.TryParse(value[..^1], out _))
        {
            value = value[..^1];
        }

        return value;
    }

    private static (string Table, string Field, FieldReferenceKind Kind) ResolveFieldReference(string queryRef, SemanticModelInput model)
    {
        foreach (var table in model.Tables)
        {
            foreach (var measure in table.Measures)
            {
                if (MatchesQueryReference(queryRef, table.Name, measure))
                {
                    return (table.Name, measure, FieldReferenceKind.Measure);
                }
            }

            foreach (var column in table.Columns)
            {
                if (MatchesQueryReference(queryRef, table.Name, column))
                {
                    return (table.Name, column, FieldReferenceKind.Column);
                }
            }
        }

        var normalized = queryRef.Replace("'", string.Empty);
        if (normalized.Contains('[') && normalized.Contains(']'))
        {
            var table = normalized[..normalized.IndexOf('[')].TrimEnd('.');
            var field = normalized[(normalized.IndexOf('[') + 1)..normalized.IndexOf(']')];
            return (table, field, FieldReferenceKind.Column);
        }

        var lastDotIndex = normalized.LastIndexOf('.');
        if (lastDotIndex > 0 && lastDotIndex < normalized.Length - 1)
        {
            return (
                normalized[..lastDotIndex],
                normalized[(lastDotIndex + 1)..],
                FieldReferenceKind.Column);
        }

        return ("Unknown", normalized, FieldReferenceKind.Column);
    }

    private static bool MatchesQueryReference(string queryRef, string tableName, string fieldName)
    {
        return string.Equals(queryRef, $"{tableName}.{fieldName}", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(queryRef, $"{tableName}[{fieldName}]", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(queryRef, $"'{tableName}'[{fieldName}]", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(queryRef, fieldName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveFromDirectory(string directoryPath)
    {
        foreach (var relativePath in CandidateBuildInputRelativePaths)
        {
            var candidatePath = Path.Combine(directoryPath, relativePath);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private sealed record ProjectContext(
        string? PbipFilePath,
        string ProjectRootPath,
        string ProjectName,
        string ReportDirectoryPath,
        string SemanticModelDirectoryPath);

    private sealed record TmdlTableLoadResult(
        IReadOnlyList<TableInput> Tables,
        IReadOnlyList<ColumnInput> Columns,
        IReadOnlyList<MeasureInput> Measures);
}
