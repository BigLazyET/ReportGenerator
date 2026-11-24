# CoverageX fork

This checkout retains ReportGenerator's upstream parsers, builders, tests and deployment assets, with changes for CoverageX integration. The upstream README below the fork notice describes that broader tool surface; it is not a promise that every format has CoverageX incremental semantics.

## Incremental contract

- Only the Cobertura parser reads `CoverageLine.ChangeLines`. Keys match normalized paths/longest suffixes; values select positive **line numbers**, including zero-hit executable lines.
- A missing change map or no overlap rejects the file. A class whose candidate files are all rejected is omitted, so it has no empty HTML page and is not counted as a missed class.
- Hits/branches are restricted to selected lines. Duplicate inputs retain existing merge rules; condition counts/percentages do not preserve Cobertura condition identifiers.
- Other parsers remain reachable and read their full input. Accepting OpenCover or another format does not establish equivalent incremental line filtering.
- The generator returns a success flag plus SummaryResult; MSBuild adapts its boolean result via `.Result`. CoverageX computes its API metrics in Core, including method/accessor counts from retained integer hits and parser-provided ranges.

The CoverageX adapter sets SourceDirs/SourcePathMappingAnchors and temporarily installs the Git-relative change map under a process-wide report lock, restoring prior state in finally. Source read leases live in CoverageX and span preparation through rendering. Standalone Cobertura callers must supply a valid map; absence does not expand to all lines.

## Maintenance

Keep upstream format dispatch, EOF/DTD behavior, source readers, report builders and valid deployment/sample assets. `src/Readme.txt`, Deployment NuGet readmes and docs/resources reports are still packaged or used as fixtures; they are not dead CoverageX documentation.

All workflows in this fork (`Engine CI`, CodeQL and dependency review) are manual-only via `workflow_dispatch`. Commit, push, pull request, schedule and completion of a change do not authorize starting or rerunning CI; an explicit user request is required. Dependency review accepts `base_ref` (default `main`) and optional `head_ref` (default selected workflow commit).

`Engine CI` builds only `ReportGenerator.Core.Test` and its project references on Windows. Its default focused filter covers Cobertura parsing/preprocessing, format detection, incremental issue regressions, analysis/merge models, local source reading, filtering, report configuration, builder selection and percentage math. Prefixes include new tests in those namespaces; new regression classes elsewhere must extend `FOCUSED_FILTER`. Failures or an empty test result fail the job. Set `full_suite=true` only when complete engine tests are requested.

The consuming CoverageX manual CI validates real HTML/Cobertura files and exact metrics; changing its engine gitlink does not start CI automatically. Upstream `ProgramTest.CreateReport` is not used as acceptance because it has no report-content assertions. Engine CI does not collect developer coverage or build/pack Console, GlobalTool, VSIX, Chocolatey or NuGet products, and does not publish or use release secrets. Existing test and deployment assets remain available for manual maintenance. CodeQL and dependency-review checks are preserved for explicit manual use.

Local developer coverage may still use the dotnet-coverage tool pinned in `.config/dotnet-tools.json` and `src/build.proj`. Coverlet dependencies and active collection scripts are removed. Existing producer-named XML fixtures and upstream format compatibility descriptions remain valid inputs, not supported collection integrations. Running this repository's own regression tests does not add user test-project orchestration to CoverageX. No NuGet/upstream release is implied by changes to this fork.
