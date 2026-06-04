namespace ReportGraph.Distribution.Models;

public sealed record InstallationGuide(
    string RepositoryCloneExample,
    string BuildCommand,
    string CliEntryPoint,
    string CodexIntegrationSummary,
    string CloudCodeIntegrationSummary,
    IReadOnlyList<string> Notes);
