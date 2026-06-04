using System.Text;
using ReportGraph.Core.Services;
using ReportGraph.Storage.Artifacts;

namespace ReportGraph.Storage.Storage;

public sealed class ReportGraphContextFileStore : IReportGraphContextFileStore
{
    public async Task SaveContextAsync(string pbipProjectPath, RenderedReportGraphContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var paths = ReportGraphPathResolver.Resolve(pbipProjectPath);
        Directory.CreateDirectory(paths.ContextDirectoryPath);
        Directory.CreateDirectory(paths.PagesDirectoryPath);

        await WriteDocumentAsync(paths.ContextDirectoryPath, context.ReportSummary, cancellationToken);
        await WriteDocumentAsync(paths.ContextDirectoryPath, context.ModelSummary, cancellationToken);
        await WriteDocumentAsync(paths.ContextDirectoryPath, context.BindingsSummary, cancellationToken);

        foreach (var pageDocument in context.PageSummaries)
        {
            await WriteDocumentAsync(paths.ContextDirectoryPath, pageDocument, cancellationToken);
        }
    }

    private static async Task WriteDocumentAsync(
        string contextDirectoryPath,
        RenderedMarkdownDocument document,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(contextDirectoryPath, document.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await File.WriteAllTextAsync(path, document.Content + Environment.NewLine, Encoding.UTF8, cancellationToken);
    }
}
