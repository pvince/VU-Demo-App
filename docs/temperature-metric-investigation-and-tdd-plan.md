# Temperature Metric Investigation and TDD Implementation Plan

## Scope
Investigate why a selected temperature metric (example: `/amdcpu/0/temperature/2`) can show invalid data in the UI and on dials, and define a test-driven, parallel implementation plan.

## Current Architecture Summary
- Sensor source: WPF app via LibreHardwareMonitor (not the Python dial server).
- Metric persistence: YAML (`sensor_identifier`, `scaling_min`, `scaling_max`).
- Runtime flow:
  1. Dial config is loaded from YAML.
  2. Identifier is resolved to a runtime sensor object.
  3. Sensor value is polled and converted to percent using min-max scaling.
  4. Percent is pushed to physical dial and rendered in UI.

## Evidence in Code
- Sensor binding for saved identifiers: `MainWindow.CreateGUIDial`.
- Unavailable binding warning path: `MainWindow.UpdateSelectedDialDisplay`.
- Runtime read path uses `sensor.Value ?? 0`: `MainWindow.RefreshDialMetricAsync`.
- Min/max scaling logic: `DialComputationEngine.ComputeDialValuePercent`.
- Identifier lookup currently exact-only: `ClassVUSensors.VU1_SensorManager.FindSensorByIdentifier`.

## Problem Signals
- UI can display a selected metric while runtime value is `[0.000]` and `0%`.
- Current logic conflates missing reading with numeric zero (`sensor.Value ?? 0`).
- Identifier lookup is brittle when hardware/driver updates change sensor identifier shape.
- Polling updates only `sensor.Hardware.Update()`, while LibreHardwareMonitor commonly uses full visitor traversal for complete refresh.

## Likely Root Causes (ranked)
1. Reading unavailable/null at poll time is interpreted as `0` and silently accepted.
2. Stale identifier after hardware topology change causes unresolved or mismatched binding.
3. Refresh strategy is incomplete for some sensor trees (sub-hardware not refreshed consistently).
4. Scaling edge cases (invalid/identical bounds) produce misleading values.

## TDD Strategy
### Red Phase (add failing tests first)
1. Add a metric polling abstraction and tests:
   - null reading should return `Unavailable`, not numeric zero.
   - `NaN` or `Infinity` should return `Unavailable`.
2. Add sensor resolution tests:
   - exact identifier match succeeds.
   - heuristic fallback by `(SensorType, SensorName, HardwareName)` resolves if identifier changed.
3. Add scaling validation tests:
   - `min >= max` returns validation failure, not implicit saturation.
4. Add UI state tests (view-model level):
   - unavailable reading displays `N/A`, warning state, and does not push dial value update.

### Green Phase (minimal implementation)
1. Introduce `SensorReadingResult` with status enum (`Available`, `Unavailable`, `Invalid`).
2. Replace `sensor.Value ?? 0` with explicit reading-state handling.
3. Add resolver fallback layer and persist canonical identifier after successful fallback.
4. Add centralized refresh method using full computer traversal (visitor) for sensor updates.
5. Add config guardrails for min-max values and non-blocking user feedback.

### Refactor Phase
1. Move metric polling and transformation out of `MainWindow` into a dedicated service.
2. Move UI data to a view-model shape (`MetricStatus`, `RawValueText`, `PercentText`, `WarningMessage`).
3. Keep `MainWindow` as orchestration only.

## Unified UI Design Direction
- Present one consistent metric status language across list, detail pane, and save actions.
- States:
  - `Connected + Valid` (value + percent shown)
  - `Connected + Unavailable` (N/A badge + retained binding note)
  - `Binding Needs Repair` (identifier stale; show quick remap CTA)
- Add a compact status strip in Configure panel:
  - Sensor status chip
  - Last refresh timestamp
  - Last valid value
- Preserve current visual style but make status color tokens explicit and reused.

## Parallel Sub-Agent Task List (Sonnet)
All tasks below are designed to run in parallel unless dependency notes say otherwise.

### Task 1: Sensor Polling Contract
- Owner: Sonnet sub-agent A
- Goal: Introduce `SensorReadingResult` and tests for null/invalid readings.
- Files likely touched:
  - `VU1WPF/Services/MetricPollingService.cs` (new)
  - `VU1WPF.Tests/MetricPollingServiceTests.cs` (new)
- Acceptance:
  - Null, NaN, and Infinity are never converted to `0` silently.

### Task 2: Sensor Resolution Robustness
- Owner: Sonnet sub-agent B
- Goal: Add fallback matching when identifier lookup fails.
- Files likely touched:
  - `VU1WPF/ClassVUSensors.cs`
  - `VU1WPF.Tests/SensorResolutionTests.cs` (new)
- Acceptance:
  - Fallback match works with renamed identifier patterns.
  - Exact match path remains unchanged.

### Task 3: Refresh Pipeline Correctness
- Owner: Sonnet sub-agent C
- Goal: Add full-tree refresh and verify polling consistency.
- Files likely touched:
  - `VU1WPF/ClassVUSensors.cs`
  - `VU1WPF/MainWindow.xaml.cs`
  - tests for refresh behavior
- Acceptance:
  - Metrics update for sensors requiring sub-hardware traversal.

### Task 4: Scaling Validation + UX Guardrails
- Owner: Sonnet sub-agent D
- Goal: enforce valid scaling bounds and clear user feedback.
- Files likely touched:
  - `VU1WPF/DialComputationEngine.cs`
  - `VU1WPF/MainWindow.xaml.cs`
  - `VU1WPF.Tests/ScalingValidationTests.cs` (new)
- Acceptance:
  - Invalid ranges are surfaced to user and blocked from save.

### Task 5: Configure Panel Unified Status UI
- Owner: Sonnet sub-agent E
- Goal: implement status strip and harmonized state visuals.
- Files likely touched:
  - `VU1WPF/MainWindow.xaml`
  - `VU1WPF/MainWindow.xaml.cs`
- Acceptance:
  - Status chip + last update + warning text displayed consistently.
  - No layout regressions on current window size.

### Task 6: Telemetry and Diagnostic Logging
- Owner: Sonnet sub-agent F
- Goal: structured logs for resolution and reading failures.
- Files likely touched:
  - `VU1WPF/MainWindow.xaml.cs`
  - `VU1WPF/ClassVUSensors.cs`
- Acceptance:
  - Log entries include dial UID, sensor identifier, status reason.

### Task 7: End-to-End Regression Tests
- Owner: Sonnet sub-agent G
- Goal: add workflow tests for save -> restart -> resolve -> update.
- Files likely touched:
  - `VU1WPF.Tests/*`
- Acceptance:
  - Reproduces prior failure and validates fixed behavior.

### Task 8: Documentation + Ops Runbook
- Owner: Sonnet sub-agent H
- Goal: user and developer troubleshooting instructions.
- Files likely touched:
  - `VU-Demo-App/docs/temperature-metric-troubleshooting.md` (new)
- Acceptance:
  - Includes symptom table, logs to check, and remediation steps.

## Suggested Execution Order
- Wave 1 (parallel): Tasks 1, 2, 4, 6
- Wave 2 (parallel): Tasks 3, 5
- Wave 3: Tasks 7, 8

## Definition of Done
- No silent coercion of unavailable sensor values to zero.
- Stale identifier recovery path implemented and tested.
- UI clearly distinguishes unavailable vs valid zero readings.
- Existing test suite remains green and new tests pass.
