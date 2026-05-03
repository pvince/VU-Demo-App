# FlaUI AutomationId + Interaction Test Parallel Task List

This task list is executable by multiple sub-agents in parallel with low merge-conflict risk.

## Scope

- Add explicit `AutomationProperties.AutomationId` to key controls in VU1WPF XAML.
- Upgrade smoke tests from window-level checks to control-interaction checks.
- Keep production behavior unchanged.

## Agent Workstreams

1. Agent A - Main window AutomationIds
- File owner: `VU1WPF/MainWindow.xaml`
- Add IDs for dial list, host/port text boxes, reconnect/about/set-rules/save buttons, metric status text, and min/max inputs.
- Output: stable selectors for primary app surface.

2. Agent B - Modal AutomationIds
- File owners: `VU1WPF/AboutWindow.xaml`, `VU1WPF/ThresholdsWindow.xaml`, `VU1WPF/SetColorWindow.xaml`
- Add IDs to close/save/delete buttons and core inputs/sliders/combos.
- Add missing `x:Name` values for unnamed ThresholdsWindow buttons before assigning AutomationIds.
- Output: stable selectors for modal workflows.

3. Agent C - Selector helper layer
- File owner: `VU1WPF/VU1WPF.Tests/UI/UiElementAssertions.cs`
- Add selector constants and strict `AutomationId` helper methods.
- Keep fallback helper (`AutomationId` or name) for transitional safety.
- Output: reusable selector abstraction for test files.

4. Agent D - Main window interaction tests
- File owner: `VU1WPF/VU1WPF.Tests/UI/MainWindowSmokeTests.cs`
- Add/upgrade tests to verify controls by AutomationId and perform basic input interactions.
- Output: deterministic startup and form interaction coverage.

5. Agent E - Modal interaction tests
- File owner: `VU1WPF/VU1WPF.Tests/UI/ModalSmokeTests.cs`
- Add tests for About open/close and Threshold dialog open with control presence checks.
- Output: deterministic dialog lifecycle coverage.

6. Agent F - Reconnect interaction tests
- File owner: `VU1WPF/VU1WPF.Tests/UI/ReconnectSmokeTests.cs`
- Add reconnect button interaction test and status-label accessibility checks.
- Output: deterministic reconnect workflow smoke coverage.

7. Agent G - Verification and hardening
- Run filtered UI tests and full suite.
- Audit duplicate AutomationIds.
- Tune waits only when race condition is demonstrated.
- Output: stable and validated UI smoke baseline.

## Sequencing

- Phase 1 in parallel: Agents A, B, C.
- Phase 2 in parallel: Agents D, E, F (after Phase 1 merge).
- Phase 3: Agent G verification and stabilization.

## Commands

From `VU1WPF`:

```powershell
dotnet build --configuration UIInvestigation
dotnet test --configuration Debug --filter "Category=UI"
dotnet test --configuration Debug
```

Duplicate AutomationId audit:

```powershell
rg "AutomationProperties.AutomationId" VU1WPF/*.xaml -n
```

## Guardrails

- Do not change polling/status semantics or config persistence behavior.
- Prefer `AutomationId` selectors; use name fallback only when migration is incomplete.
- Keep tests bounded with explicit retry timeouts.
- Keep UI tests in `VU1WPF/VU1WPF.Tests/UI/`.
