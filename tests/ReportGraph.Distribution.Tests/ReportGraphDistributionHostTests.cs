using ReportGraph.Distribution.Services;

namespace ReportGraph.Distribution.Tests;

public sealed class ReportGraphDistributionHostTests
{
    private readonly ReportGraphDistributionHost _host = new();

    [Fact]
    public void GetCliCommands_ShouldExposeExpectedCommandSet()
    {
        var commands = _host.GetCliCommands();

        Assert.Contains(commands, command => command.Name == "init");
        Assert.Contains(commands, command => command.Name == "update");
        Assert.Contains(commands, command => command.Name == "delete");
        Assert.Contains(commands, command => command.Name == "status");
        Assert.Contains(commands, command => command.Name == "query");
        Assert.Contains(commands, command => command.Name == "mcp");
        Assert.Contains(commands, command => command.Name == "install-info");
    }

    [Fact]
    public void GetMcpTools_ShouldExposeExpectedToolSet()
    {
        var tools = _host.GetMcpTools();

        Assert.Contains(tools, tool => tool.Name == "report.graph.load");
        Assert.Contains(tools, tool => tool.Name == "report.page.get");
        Assert.Contains(tools, tool => tool.Name == "report.page.bindings");
        Assert.Contains(tools, tool => tool.Name == "report.model.table.get");
        Assert.Contains(tools, tool => tool.Name == "report.visual.get");
        Assert.Contains(tools, tool => tool.Name == "report.graph.explore");
    }

    [Fact]
    public void GetInstallationGuide_ShouldDescribeGitAndHostEntryPoints()
    {
        var guide = _host.GetInstallationGuide();

        Assert.Contains("git clone", guide.RepositoryCloneExample, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet build ReportGraph.slnx", guide.BuildCommand, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src/ReportGraph.Cli", guide.CliEntryPoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Codex", guide.CodexIntegrationSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CloudCode", guide.CloudCodeIntegrationSummary, StringComparison.OrdinalIgnoreCase);
    }
}
