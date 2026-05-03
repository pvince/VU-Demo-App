---
description: "Use when editing VU1WPF WPF app code, XAML, sensor polling, dial updates, threshold logic, YAML config, or xUnit tests. Covers ownership boundaries, status handling, and verification."
name: "VU1WPF App Guidance"
applyTo:
  - "VU1WPF/**/*.cs"
  - "VU1WPF/**/*.xaml"
---

# VU1WPF App Guidance

- Link to existing docs instead of restating them: [README](../../README.md) for setup, [ARCHITECTURE](../../ARCHITECTURE.md) for design and config schema, [temperature metric investigation and TDD plan](../../docs/temperature-metric-investigation-and-tdd-plan.md) for recent sensor-status rationale, and [build-project skill](../skills/build-project/SKILL.md) for build, test, and publish flows.
- Keep the polling pipeline split by responsibility. `MetricPollingService` polls and reports `(SensorStatus, decimal?)`. `ClassVUSensors` resolves identifiers with exact match first and heuristic fallback second. `DialComputationEngine` scales values and resolves threshold colors. `DialUpdateOrchestrator` pushes HTTP updates without blocking the poll loop.
- Preserve explicit status semantics. Do not replace unavailable, stale, invalid, or missing sensor reads with numeric `0` unless the task explicitly changes that policy end to end.
- Preserve graceful degradation. VU Server failures should surface as status or logging and allow the app to keep polling locally.
- Configuration work belongs in `ClassConfigurationManager` and `DebouncedConfigSaver`; keep YAML compatibility and debounce behavior intact unless the task calls for schema or persistence changes.
- Keep existing naming and namespace patterns unless the task is explicit cleanup. Legacy `Class*` names are normal in this codebase.
- When behavior changes, update targeted xUnit coverage in `VU1WPF/VU1WPF.Tests/`. Prefer focused runs during iteration such as `dotnet test --filter "FullyQualifiedName~ClassDialGUITests"` from `VU1WPF/`.
- For WPF UI debugging that must avoid admin elevation, build `UIInvestigation` rather than changing manifests or elevation behavior ad hoc.