# ReportGraph Git Upload Contents

This folder is a structured upload copy of the ReportGraph workspace.

## Root Files

- `README.md`: repository entry guide and quick start.
- `ReportGraph.slnx`: solution entrypoint.
- `codex-install.json`: machine-readable install descriptor for Codex-style automated setup.
- `reportgraph.ps1` / `reportgraph.cmd`: local wrapper commands.
- `install-reportgraph.ps1` / `install-reportgraph.cmd`: optional .NET tool install scripts.
- `publish-reportgraph.ps1` / `publish-reportgraph.cmd`: recommended self-contained publish scripts.
- `.gitignore`: excludes build outputs and generated graph artifacts.

## Source And Tests

- `src/`: ReportGraph source projects.
- `tests/`: unit, integration, distribution, CLI, and MCP validation tests.

## Documentation

- `docs/功能清单.md`: public feature list.
- `docs/使用说明.md`: public usage guide.
- `docs/安装与接入说明.md`: public installation and integration guide.

Internal requirement notes, implementation planning notes, and development task checklists are intentionally excluded from this Git upload package.

## Input Projects

This upload package does not include business PBIP projects.

Use your own PBIP project when running `reportgraph init`, `reportgraph update`, and `reportgraph query`. Generated `Graph/` outputs and Power BI local `.pbi/` state files should stay outside Git.

## Packaged Output

- `release/win-x64/reportgraph.exe`: current self-contained packaged executable copied from `artifacts/publish/win-x64/reportgraph.exe`.

The recommended automated install path remains:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-reportgraph.ps1
.\artifacts\publish\win-x64\reportgraph.exe --help
```

The `release/` executable is included as a convenient packaged snapshot, while `publish-reportgraph.ps1` is the reproducible build path.
