using ReportGraph.Adapters.Services;
using ReportGraph.Core.Models;
using ReportGraph.Storage.Serialization;

namespace ReportGraph.Distribution.Tests;

public sealed class ReportGraphProjectAdapterTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "ReportGraphProjectAdapterTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_ShouldLoadFromDirectJsonPath()
    {
        var projectPath = Path.Combine(tempRoot, "DirectJsonProject");
        var inputPath = await WriteBuildInputAsync(projectPath);
        var adapter = new ReportGraphProjectAdapter();

        var input = await adapter.LoadAsync(inputPath);

        Assert.Equal(projectPath, input.Source.PbipProjectPath);
        Assert.Equal("Sales", input.Report.ReportName);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadFromProjectDirectory()
    {
        var projectPath = Path.Combine(tempRoot, "DirectoryProject");
        await WriteBuildInputAsync(projectPath);
        var adapter = new ReportGraphProjectAdapter();

        var input = await adapter.LoadAsync(projectPath);

        Assert.Equal(projectPath, input.Source.PbipProjectPath);
        Assert.Equal("Page1", input.Report.ActivePageId);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadFromPbipFilePath()
    {
        var projectPath = Path.Combine(tempRoot, "PbipProject");
        await WriteBuildInputAsync(projectPath);
        var pbipPath = Path.Combine(projectPath, "Sales.pbip");
        await File.WriteAllTextAsync(pbipPath, "{}");
        var adapter = new ReportGraphProjectAdapter();

        var input = await adapter.LoadAsync(pbipPath);

        Assert.Equal(projectPath, input.Source.PbipProjectPath);
        Assert.Equal("Sales Model", input.Model.ModelName);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnFriendlyError_WhenPbixIsProvided()
    {
        var projectPath = Path.Combine(tempRoot, "PbixProject");
        Directory.CreateDirectory(projectPath);
        var pbixPath = Path.Combine(projectPath, "Sales.pbix");
        await File.WriteAllTextAsync(pbixPath, "fake-pbix");
        var adapter = new ReportGraphProjectAdapter();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => adapter.LoadAsync(pbixPath));

        Assert.Contains("not a supported input", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Power BI Desktop", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PBIP", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_ShouldParsePbipProjectStructure_WhenBuildInputIsMissing()
    {
        var projectPath = Path.Combine(tempRoot, "DirectPbipProject");
        var pbipPath = await WritePbipProjectAsync(projectPath);
        var adapter = new ReportGraphProjectAdapter();

        var input = await adapter.LoadAsync(pbipPath);

        Assert.Equal(projectPath, input.Source.PbipProjectPath);
        Assert.Equal("Sales", input.Report.ReportName);
        Assert.Equal("Page1", input.Report.ActivePageId);
        Assert.Single(input.Report.Pages);
        Assert.Single(input.Report.Pages[0].Visuals);
        Assert.Equal("Visual1", input.Report.Pages[0].Visuals[0].VisualId);
        Assert.Equal("card", input.Report.Pages[0].Visuals[0].VisualType);
        Assert.Single(input.Model.Tables);
        Assert.Equal("FactSales", input.Model.Tables[0].Name);
        Assert.Single(input.Model.Tables[0].Measures);
        Assert.Equal("Sales Amount", input.Model.Tables[0].Measures[0]);
        Assert.NotNull(input.Model.Measures);
        Assert.Contains(input.Model.Measures!, measure =>
            measure.Table == "FactSales" &&
            measure.Name == "Sales Amount" &&
            measure.DisplayFolder == "Sales" &&
            measure.Expression == "SUM('FactSales'[Amount])");
        Assert.NotNull(input.Model.Columns);
        Assert.Contains(input.Model.Columns!, column =>
            column.Table == "FactSales" &&
            column.Name == "Amount" &&
            column.DisplayFolder == "Sales");
        Assert.NotNull(input.SourceFiles);
        Assert.Contains(input.SourceFiles!, file => file.Path == "Sales.pbip");
        Assert.Contains(input.SourceFiles!, file => file.Path == "Sales.Report/definition/pages/Page1/visuals/Visual1/visual.json");
        Assert.Contains(input.SourceFiles!, file => file.Path == "Sales.SemanticModel/model.bim");
        Assert.Contains(input.SourceFiles!, file => file.Path == "docs/sales-guide.md");
        Assert.DoesNotContain(input.SourceFiles!, file => file.Path.StartsWith("Graph/", StringComparison.OrdinalIgnoreCase));
        var document = Assert.Single(input.Documents!);
        Assert.Equal("docs/sales-guide.md", document.Path);
        Assert.Contains("Sales Amount", document.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_ShouldParseTmdlSemanticModel_WhenModelBimIsMissing()
    {
        var projectPath = Path.Combine(tempRoot, "TmdlPbipProject");
        var pbipPath = await WriteTmdlPbipProjectAsync(projectPath);
        var adapter = new ReportGraphProjectAdapter();

        var input = await adapter.LoadAsync(pbipPath);

        Assert.Equal(projectPath, input.Source.PbipProjectPath);
        Assert.Equal("Sales Model", input.Model.ModelName);
        Assert.Equal(2, input.Model.Tables.Count);
        Assert.Contains(input.Model.Tables, table => table.Name == "FactSales" && table.Measures.Contains("Sales Amount"));
        Assert.Contains(input.Model.Tables, table => table.Name == "DimDate" && table.Columns.Contains("DateKey"));
        Assert.Single(input.Model.Relationships);
        Assert.Equal("SalesToDate", input.Model.Relationships[0].RelationshipId);
        Assert.Equal("FactSales", input.Model.Relationships[0].FromTable);
        Assert.Equal("DimDate", input.Model.Relationships[0].ToTable);
        Assert.NotNull(input.Model.Measures);
        Assert.Contains(input.Model.Measures!, measure =>
            measure.Table == "FactSales" &&
            measure.Name == "Sales Amount" &&
            measure.DisplayFolder == "Sales");
        Assert.NotNull(input.Model.Columns);
        Assert.Contains(input.Model.Columns!, column =>
            column.Table == "FactSales" &&
            column.Name == "Amount" &&
            column.DisplayFolder == "Sales");
    }

    [Fact]
    public async Task LoadAsync_ShouldUseStableGeneratedAtUtc_WhenPbipInputsAreUnchanged()
    {
        var projectPath = Path.Combine(tempRoot, "StableGeneratedAtProject");
        var pbipPath = await WritePbipProjectAsync(projectPath);
        var adapter = new ReportGraphProjectAdapter();

        var first = await adapter.LoadAsync(pbipPath);
        await Task.Delay(50);
        var second = await adapter.LoadAsync(pbipPath);

        Assert.Equal(first.GeneratedAtUtc, second.GeneratedAtUtc);
    }

    [Fact]
    public async Task LoadAsync_ShouldParseVisualFilterSelections_FromPbipVisualDefinition()
    {
        var projectPath = Path.Combine(tempRoot, "SlicerFilterProject");
        var pbipPath = await WriteSlicerPbipProjectAsync(projectPath);
        var adapter = new ReportGraphProjectAdapter();

        var input = await adapter.LoadAsync(pbipPath);

        var visual = Assert.Single(input.Report.Pages[0].Visuals);
        Assert.Equal("slicer", visual.VisualType);
        Assert.NotNull(visual.Filters);
        var filter = Assert.Single(visual.Filters!);
        Assert.Equal("DimDate", filter.Table);
        Assert.Equal("Year", filter.Field);
        Assert.Equal(["2023", "2024"], filter.Values);
    }

    [Fact]
    public async Task LoadAsync_ShouldIgnoreHiddenLocalStateFiles_WhenResolvingGeneratedAtUtc()
    {
        var projectPath = Path.Combine(tempRoot, "HiddenStateProject");
        var pbipPath = await WritePbipProjectAsync(projectPath);
        var hiddenLocalStatePath = Path.Combine(projectPath, "Sales.SemanticModel", ".pbi", "localSettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(hiddenLocalStatePath)!);
        await File.WriteAllTextAsync(hiddenLocalStatePath, "{}");

        var adapter = new ReportGraphProjectAdapter();
        var first = await adapter.LoadAsync(pbipPath);

        await Task.Delay(50);
        File.SetLastWriteTimeUtc(hiddenLocalStatePath, DateTime.UtcNow);

        var second = await adapter.LoadAsync(pbipPath);

        Assert.Equal(first.GeneratedAtUtc, second.GeneratedAtUtc);
        Assert.NotNull(second.SourceFiles);
        Assert.DoesNotContain(second.SourceFiles!, file => file.Path.Contains(".pbi/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadAsync_ShouldTrackMarkdownChangesInSourceFiles()
    {
        var projectPath = Path.Combine(tempRoot, "MarkdownSourceProject");
        var pbipPath = await WritePbipProjectAsync(projectPath);
        var documentPath = Path.Combine(projectPath, "docs", "sales-guide.md");
        var adapter = new ReportGraphProjectAdapter();

        var first = await adapter.LoadAsync(pbipPath);
        await File.WriteAllTextAsync(
            documentPath,
            """
            # Sales Guide

            Updated guidance for Sales Amount on the Overview page.
            """);
        var second = await adapter.LoadAsync(pbipPath);

        var firstHash = first.SourceFiles!.Single(file => file.Path == "docs/sales-guide.md").ContentHash;
        var secondHash = second.SourceFiles!.Single(file => file.Path == "docs/sales-guide.md").ContentHash;

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void SourceArtifactPathRules_ShouldExcludeGeneratedAndLocalStatePaths()
    {
        var projectPath = Path.Combine(tempRoot, "PathRulesProject");

        Assert.True(ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, Path.Combine(projectPath, "Sales.pbip")));
        Assert.True(ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, Path.Combine(projectPath, "docs", "notes.md")));
        Assert.True(ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, Path.Combine(projectPath, "Sales.Report", "definition", "pages", "Page1", "page.json")));
        Assert.False(ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, Path.Combine(projectPath, "Graph", "report-graph.json")));
        Assert.False(ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, Path.Combine(projectPath, "Sales.SemanticModel", ".pbi", "localSettings.json")));
        Assert.False(ReportGraphSourceArtifactPathRules.IsTrackedSourceFile(projectPath, Path.Combine(projectPath, "bin", "debug.log")));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task<string> WriteBuildInputAsync(string projectPath)
    {
        Directory.CreateDirectory(projectPath);

        var input = new ReportGraphBuildInput(
            Version: "1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            Source: new ReportGraphSource(
                InstanceId: "instance-1",
                PbipProjectPath: projectPath,
                ReportRootPath: Path.Combine(projectPath, "Report"),
                ModelName: "Sales Model"),
            Report: new ReportInput(
                ReportName: "Sales",
                ActivePageId: "Page1",
                PagesLastModifiedUtc: new DateTimeOffset(2026, 6, 3, 10, 20, 0, TimeSpan.Zero),
                Pages:
                [
                    new ReportPageInput(
                        PageId: "Page1",
                        DisplayName: "Overview",
                        Ordinal: 0,
                        Visuals:
                        [
                            new VisualInput(
                                VisualId: "Visual1",
                                VisualType: "card",
                                Fields:
                                [
                                    new VisualFieldInput("Value", "FactSales", "Sales Amount", FieldReferenceKind.Measure)
                                ])
                        ])
                ]),
            Model: new SemanticModelInput(
                ModelName: "Sales Model",
                Tables:
                [
                    new TableInput("FactSales", false, ["SalesId"], ["Sales Amount"])
                ],
                Relationships: []));

        var inputPath = Path.Combine(projectPath, "report-graph.build-input.json");
        await File.WriteAllTextAsync(inputPath, ReportGraphJson.Serialize(input));
        return inputPath;
    }

    private static async Task<string> WritePbipProjectAsync(string projectPath)
    {
        Directory.CreateDirectory(projectPath);

        var pbipPath = Path.Combine(projectPath, "Sales.pbip");
        var reportDirectoryPath = Path.Combine(projectPath, "Sales.Report");
        var semanticModelDirectoryPath = Path.Combine(projectPath, "Sales.SemanticModel");
        var definitionDirectoryPath = Path.Combine(reportDirectoryPath, "definition");
        var pagesDirectoryPath = Path.Combine(definitionDirectoryPath, "pages");
        var pageDirectoryPath = Path.Combine(pagesDirectoryPath, "Page1");
        var visualsDirectoryPath = Path.Combine(pageDirectoryPath, "visuals", "Visual1");

        Directory.CreateDirectory(visualsDirectoryPath);
        Directory.CreateDirectory(semanticModelDirectoryPath);
        Directory.CreateDirectory(Path.Combine(projectPath, "docs"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Graph", "context"));

        await File.WriteAllTextAsync(
            pbipPath,
            """
            {
              "version": "1.0",
              "artifacts": [
                {
                  "report": {
                    "path": "Sales.Report"
                  }
                }
              ]
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(reportDirectoryPath, "definition.pbir"),
            """
            {
              "version": "4.0",
              "datasetReference": {
                "byPath": {
                  "path": "../Sales.SemanticModel"
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pagesDirectoryPath, "pages.json"),
            """
            {
              "pageOrder": ["Page1"],
              "activePageName": "Page1"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pageDirectoryPath, "page.json"),
            """
            {
              "name": "Page1",
              "displayName": "Overview"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(visualsDirectoryPath, "visual.json"),
            """
            {
              "name": "Visual1",
              "visual": {
                "visualType": "card",
                "query": {
                  "queryState": {
                    "Value": {
                      "projections": [
                        {
                          "queryRef": "FactSales[Sales Amount]"
                        }
                      ]
                    }
                  }
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(semanticModelDirectoryPath, "model.bim"),
            """
            {
              "name": "Sales Model",
              "model": {
                "tables": [
                  {
                    "name": "FactSales",
                    "columns": [
                      { "name": "SalesId" },
                      { "name": "Amount", "displayFolder": "Sales", "formatString": "#,0" }
                    ],
                    "measures": [
                      { "name": "Sales Amount", "displayFolder": "Sales", "formatString": "#,0", "expression": "SUM('FactSales'[Amount])" }
                    ]
                  }
                ],
                "relationships": []
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "docs", "sales-guide.md"),
            """
            # Sales Guide

            Use Sales Amount on the Overview page.
            """);

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "Graph", "context", "generated.md"),
            """
            # Generated

            This generated context file should not be scanned as source documentation.
            """);

        return pbipPath;
    }

    private static async Task<string> WriteTmdlPbipProjectAsync(string projectPath)
    {
        Directory.CreateDirectory(projectPath);

        var pbipPath = Path.Combine(projectPath, "Sales.pbip");
        var reportDirectoryPath = Path.Combine(projectPath, "Sales.Report");
        var semanticModelDirectoryPath = Path.Combine(projectPath, "Sales.SemanticModel");
        var semanticDefinitionDirectoryPath = Path.Combine(semanticModelDirectoryPath, "definition");
        var semanticTablesDirectoryPath = Path.Combine(semanticDefinitionDirectoryPath, "tables");
        var pagesDirectoryPath = Path.Combine(reportDirectoryPath, "definition", "pages");
        var visualDirectoryPath = Path.Combine(pagesDirectoryPath, "Page1", "visuals", "Visual1");

        Directory.CreateDirectory(visualDirectoryPath);
        Directory.CreateDirectory(semanticTablesDirectoryPath);

        await File.WriteAllTextAsync(
            pbipPath,
            """
            {
              "version": "1.0",
              "artifacts": [
                {
                  "report": {
                    "path": "Sales.Report"
                  }
                }
              ]
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(reportDirectoryPath, "definition.pbir"),
            """
            {
              "version": "4.0",
              "datasetReference": {
                "byPath": {
                  "path": "../Sales.SemanticModel"
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pagesDirectoryPath, "pages.json"),
            """
            {
              "pageOrder": ["Page1"],
              "activePageName": "Page1"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pagesDirectoryPath, "Page1", "page.json"),
            """
            {
              "name": "Page1",
              "displayName": "Overview"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(visualDirectoryPath, "visual.json"),
            """
            {
              "name": "Visual1",
              "visual": {
                "visualType": "card",
                "query": {
                  "queryState": {
                    "Value": {
                      "projections": [
                        {
                          "queryRef": "FactSales[Sales Amount]"
                        }
                      ]
                    }
                  }
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(semanticDefinitionDirectoryPath, "database.tmdl"),
            """
            database 'Sales Model'
            """);

        await File.WriteAllTextAsync(
            Path.Combine(semanticTablesDirectoryPath, "FactSales.tmdl"),
            """
            table FactSales
                column SalesId
                column DateKey
                column Amount
                    displayFolder: Sales
                    formatString: #,0
                measure 'Sales Amount' = SUM('FactSales'[Amount])
                    displayFolder: Sales
                    formatString: #,0
            """);

        await File.WriteAllTextAsync(
            Path.Combine(semanticTablesDirectoryPath, "DimDate.tmdl"),
            """
            table DimDate
                column DateKey
            """);

        await File.WriteAllTextAsync(
            Path.Combine(semanticDefinitionDirectoryPath, "relationships.tmdl"),
            """
            relationship SalesToDate
                fromColumn: FactSales[DateKey]
                toColumn: DimDate[DateKey]
                isActive: true
            """);

        return pbipPath;
    }

    private static async Task<string> WriteSlicerPbipProjectAsync(string projectPath)
    {
        Directory.CreateDirectory(projectPath);

        var pbipPath = Path.Combine(projectPath, "Sales.pbip");
        var reportDirectoryPath = Path.Combine(projectPath, "Sales.Report");
        var semanticModelDirectoryPath = Path.Combine(projectPath, "Sales.SemanticModel");
        var definitionDirectoryPath = Path.Combine(reportDirectoryPath, "definition");
        var pagesDirectoryPath = Path.Combine(definitionDirectoryPath, "pages");
        var pageDirectoryPath = Path.Combine(pagesDirectoryPath, "Page1");
        var visualsDirectoryPath = Path.Combine(pageDirectoryPath, "visuals", "Visual1");

        Directory.CreateDirectory(visualsDirectoryPath);
        Directory.CreateDirectory(semanticModelDirectoryPath);

        await File.WriteAllTextAsync(
            pbipPath,
            """
            {
              "version": "1.0",
              "artifacts": [
                {
                  "report": {
                    "path": "Sales.Report"
                  }
                }
              ]
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(reportDirectoryPath, "definition.pbir"),
            """
            {
              "version": "4.0",
              "datasetReference": {
                "byPath": {
                  "path": "../Sales.SemanticModel"
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pagesDirectoryPath, "pages.json"),
            """
            {
              "pageOrder": ["Page1"],
              "activePageName": "Page1"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(pageDirectoryPath, "page.json"),
            """
            {
              "name": "Page1",
              "displayName": "Overview"
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(visualsDirectoryPath, "visual.json"),
            """
            {
              "name": "Visual1",
              "visual": {
                "visualType": "slicer",
                "query": {
                  "queryState": {
                    "Values": {
                      "projections": [
                        {
                          "queryRef": "DimDate.Year"
                        }
                      ]
                    }
                  }
                },
                "objects": {
                  "general": [
                    {
                      "properties": {
                        "filter": {
                          "filter": {
                            "Version": 2,
                            "From": [
                              {
                                "Name": "d",
                                "Entity": "DimDate",
                                "Type": 0
                              }
                            ],
                            "Where": [
                              {
                                "Condition": {
                                  "In": {
                                    "Expressions": [
                                      {
                                        "Column": {
                                          "Expression": {
                                            "SourceRef": {
                                              "Source": "d"
                                            }
                                          },
                                          "Property": "Year"
                                        }
                                      }
                                    ],
                                    "Values": [
                                      [
                                        {
                                          "Literal": {
                                            "Value": "2023L"
                                          }
                                        }
                                      ],
                                      [
                                        {
                                          "Literal": {
                                            "Value": "2024L"
                                          }
                                        }
                                      ]
                                    ]
                                  }
                                }
                              }
                            ]
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(semanticModelDirectoryPath, "model.bim"),
            """
            {
              "name": "Sales Model",
              "model": {
                "tables": [
                  {
                    "name": "DimDate",
                    "columns": [
                      { "name": "Year" }
                    ],
                    "measures": []
                  }
                ],
                "relationships": []
              }
            }
            """);

        return pbipPath;
    }
}
