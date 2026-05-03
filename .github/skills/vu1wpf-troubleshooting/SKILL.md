---
name: vu1wpf-troubleshooting
description: "Troubleshoot VU1WPF sensor lookup failures, SensorStatus regressions, localhost dial delivery issues, and UIInvestigation elevation/debugging problems. Use when: metric reads as 0, dial not updating, server unreachable, or the app only works as admin."
argument-hint: "sensor | status | dial-delivery | ui-investigation"
---

# Troubleshoot VU1WPF

## When to Use

- Metric reads as `0` unexpectedly.
- `SensorStatus` handling regressed.
- Dials stop updating over localhost or only some updates land.
- App behavior differs between elevated and non-elevated runs.

## References

- [ARCHITECTURE](../../../ARCHITECTURE.md)
- [temperature metric investigation and TDD plan](../../../docs/temperature-metric-investigation-and-tdd-plan.md)
- [build-project skill](../build-project/SKILL.md)

## Procedure

1. Confirm scope first: sensor discovery, status propagation, dial HTTP delivery, config or auth, or elevation and UI behavior.
2. Reproduce with the least moving parts. Build from `VU1WPF/`. Use `UIInvestigation` when the point is UI or non-admin behavior instead of changing manifests ad hoc.
3. For sensor lookup issues, inspect `ClassVUSensors.cs` and `MetricPollingService.cs`. Keep exact identifier match first, heuristic fallback second, and explicit `(SensorStatus, decimal?)` results.
4. For `SensorStatus` regressions, trace data from polling through computation and UI. Do not hide unavailable or invalid sensors by coercing them to numeric `0`.
5. For dial delivery issues, inspect `ClassVUServer.cs`, `DialUpdateOrchestrator.cs`, and config values for `serverHost`, `serverPort`, `masterKey`, and `dialUpdatePeriod`. Keep failed HTTP calls non-blocking.
6. For localhost API mismatches, compare the client contract with `VU-Server/server.py`. Keep `/api/v0/`, query parameter names, UID handling, and 0-100 value scales synchronized.
7. Start with focused xUnit coverage before broad refactors. Prefer `SensorResolutionTests`, `MetricPollingServiceTests`, `EndToEndRegressionTests`, and filtered `dotnet test` runs while iterating.
8. Report root cause, proof, and the smallest safe fix. Call out whether the issue is code, configuration, environment, or server availability.