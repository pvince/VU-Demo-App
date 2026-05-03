# FlaUI UI Test Parallel Task List

This task list is designed for parallel sub-agent execution with minimal merge conflicts.

## Scope

- Add a UI smoke-test scaffold to VU1WPF.Tests using FlaUI UIA3.
- Keep production logic behavior unchanged.
- Keep existing xUnit unit/integration tests stable.

## Parallel Workstreams

1. Workstream A: Project wiring
- Update VU1WPF.Tests package references for FlaUI.
- Confirm target framework remains net8.0-windows.
- Confirm UI tests are marked with Trait Category=UI.
- Output: buildable test project with FlaUI dependencies.

2. Workstream B: Fixture and lifecycle
- Implement shared fixture for launching VU1-Demo-App executable.
- Add deterministic app shutdown and process cleanup.
- Add app binary resolution via env var VU1WPF_UI_APP_PATH, then fallback paths.
- Output: reusable fixture usable by all UI smoke tests.

3. Workstream C: UI helper layer
- Add helper methods for AutomationId-based element lookup.
- Add helper wait/retry for top-level window detection.
- Keep helpers assertion-friendly for readable failures.
- Output: centralized selectors and waits.

4. Workstream D: Smoke tests for startup
- Add smoke test for main window title.
- Add smoke test for core controls existence.
- Mark each with Trait Category=UI.
- Output: startup regression coverage.

5. Workstream E: Smoke tests for secondary window
- Add smoke test for About window open and close flow.
- Ensure test closes opened windows before completion.
- Output: basic modal/top-level interaction coverage.

6. Workstream F: Verification and hardening
- Build UIInvestigation configuration.
- Run dotnet test filtered to Category=UI.
- Run full test suite after UI pass.
- Capture flaky selectors and adjust waits only where race is confirmed.
- Output: validated scaffold and reliability notes.

## Sequencing

- Start A, B, C in parallel.
- Start D and E when B and C are complete.
- Run F after D and E are merged.

## Command Baseline

From VU1WPF directory:

```powershell
dotnet restore
dotnet build --configuration UIInvestigation
dotnet test --configuration Debug --filter "Category=UI"
dotnet test --configuration Debug
```

## Guardrails

- Do not coerce unavailable sensor states to numeric 0.
- Do not alter MetricPollingService, DialUpdateOrchestrator, or config persistence behavior for scaffold-only work.
- Keep UI tests in dedicated files/folders under VU1WPF.Tests.
- Prefer AutomationId selectors over visible text selectors.
- Keep per-test runtime bounded with explicit waits/timeouts.
