namespace ReportGraph.Core.Services;

public interface IReportGraphFingerprintService
{
    string CreateModelFingerprint(ModelFingerprintInput input);
    string CreateReportFingerprint(ReportFingerprintInput input);
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
