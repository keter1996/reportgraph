using ReportGraph.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace ReportGraph.Core.Services;

public interface IReportGraphFingerprintService
{
    string CreateModelFingerprint(ModelFingerprintInput input);
    string CreateReportFingerprint(ReportFingerprintInput input);
    string? CreateSourceFingerprint(IReadOnlyList<SourceArtifactInput>? sourceFiles);
}

public sealed class ReportGraphFingerprintService : IReportGraphFingerprintService
{
    public string CreateModelFingerprint(ModelFingerprintInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return string.Join(
            "|",
            input.ModelName.Trim(),
            input.TableCount,
            input.ColumnCount,
            input.MeasureCount,
            input.RelationshipCount);
    }

    public string CreateReportFingerprint(ReportFingerprintInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return string.Join(
            "|",
            input.PagesLastWriteUtc.ToUniversalTime().ToString("O"),
            input.PageCount,
            input.VisualCount);
    }

    public string? CreateSourceFingerprint(IReadOnlyList<SourceArtifactInput>? sourceFiles)
    {
        if (sourceFiles is null || sourceFiles.Count == 0)
        {
            return null;
        }

        var orderedFiles = sourceFiles
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(file => $"{NormalizePath(file.Path)}|{file.ContentHash}")
            .ToArray();

        var payload = string.Join('\n', orderedFiles);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return $"sha256:{Convert.ToHexStringLower(hash)}";
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');
}

public sealed record ModelFingerprintInput(
    string ModelName,
    int TableCount,
    int ColumnCount,
    int MeasureCount,
    int RelationshipCount);

public sealed record ReportFingerprintInput(
    DateTimeOffset PagesLastWriteUtc,
    int PageCount,
    int VisualCount);
