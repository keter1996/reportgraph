using ReportGraph.Distribution.Models;

namespace ReportGraph.Distribution.Services;

public interface IReportGraphDistributionHost
{
    IReadOnlyList<CliCommandDefinition> GetCliCommands();

    IReadOnlyList<McpToolDefinition> GetMcpTools();

    InstallationGuide GetInstallationGuide();
}
