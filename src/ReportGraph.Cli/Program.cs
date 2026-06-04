using ReportGraph.Cli;
using ReportGraph.Adapters.Services;
using ReportGraph.Core.Services;
using ReportGraph.Distribution.Services;
using ReportGraph.HostIntegration.Services;
using ReportGraph.Query.Services;
using ReportGraph.Storage.Storage;

var runner = new CliRunner(
    buildInputAdapter: new ReportGraphProjectAdapter(),
    distributionHost: new ReportGraphDistributionHost(),
    graphService: new ReportGraphService(
        builder: new ReportGraphBuilder(),
        renderer: new ReportGraphContextRenderer(),
        fingerprintService: new ReportGraphFingerprintService(),
        stalenessChecker: new ReportGraphStalenessChecker(),
        fileStore: new ReportGraphFileStore(),
        contextFileStore: new ReportGraphContextFileStore()),
    queryService: new ReportGraphQueryService(),
    fileStore: new ReportGraphFileStore());

return await runner.RunAsync(args);
