# Disconnect Recovery Handoff Sequencing Sheet

## Mission
Stabilize dial updates after disconnect and improve troubleshooting fidelity while controlling log bloat.

## Non-Negotiable Constraints
1. Do not change API route contracts under `/api/v0`.
2. Preserve backward compatibility for existing config files.
3. Keep default runtime logging conservative.
4. All behavior changes must be covered by tests.

## Delivery Order
1. Phase A: Test harness expansion (must merge first).
2. Phase B: Client reliability hardening (parallel with Phase C after A).
3. Phase C: Server reliability hardening (parallel with Phase B after A).
4. Phase D: Client diagnostics logging controls (can begin after B starts, merge after B).
5. Phase E: Integration verification and release checklist.

## Sub-Agent Assignments

### Sub-Agent A (Sonnet) - Client Reliability Core
Prompt:
You are working in `VU-Demo-App/VU1WPF` and `VU-Demo-App/VU1WPF/VU1WPF.Tests`.

Objective:
- Eliminate client-side dial update stalls from network disconnects.
- Ensure one dial failure does not starve other dials.

Scope:
- `ClassVUServer.cs`
- `MainWindow.xaml.cs`
- `DialUpdateOrchestrator.cs`
- `ClassVUServerTests.cs`
- `UnitTest1.cs`

Required work:
1. Add explicit HTTP timeout handling for async dial API calls.
2. Ensure cancellation and timeout paths return safely without deadlock.
3. In `ProcessDialUpdatesAsync`, isolate per-dial failures/timeouts so one dial does not block all dials.
4. Keep single-flight behavior of orchestrator intact.
5. Add/adjust tests for timeout and cancellation behavior.

Acceptance criteria:
- Timeout tests pass and fail fast under delayed handlers.
- Orchestrator remains usable after timeout/cancel.
- No API route/path/parameter changes.

Deliverables:
- Patch set.
- Test list and results.
- Residual risk note (concurrency assumptions).

### Sub-Agent B (Sonnet) - Server Reliability Core
Prompt:
You are working in `VU-Server`.

Objective:
- Prevent silent update loss and improve runtime resilience under serial and DB contention failures.

Scope:
- `server_dial_handler.py`
- `database.py`
- `server_config.py`
- `server.py`
- tests or deterministic harness script in-repo

Required work:
1. In periodic dial update routines, clear changed flags only on confirmed send success.
2. Preserve queued updates for retry on command failure.
3. Add exception containment in periodic update loop so a single serial exception does not collapse update processing.
4. Add bounded retry/backoff for sqlite `database is locked` startup path.
5. Add targeted logging with dial UID/index and operation context.

Acceptance criteria:
- Failed send retains queued state.
- Periodic loop continues after transient errors.
- Startup handles transient sqlite lock with bounded retry and clear terminal error.

Deliverables:
- Patch set.
- Repro/harness results.
- Retry/backoff rationale.

### Sub-Agent C (Haiku) - Client Config + Logging Controls
Prompt:
You are working in `VU-Demo-App/VU1WPF` and related tests.

Objective:
- Add optional logging controls without breaking existing config files.

Scope:
- `AppConfigClassTemplate.cs`
- `ClassConfigurationManager.cs`
- `MainWindow.xaml.cs`
- `ConfigurationManagerTests.cs`

Required work:
1. Add optional config keys (example): `log_level`, `diagnostics_mode`.
2. Implement strict parsing with safe defaults on invalid values.
3. Wire logger initialization to use configured level.
4. Keep file rolling protections and bounded retention.
5. Add tests for config parsing/default behavior.

Acceptance criteria:
- Existing configs load with defaults.
- Invalid values fall back safely.
- Default runtime logging remains conservative.

Deliverables:
- Patch set.
- Test list and results.
- Config migration note.

### Sub-Agent D (Haiku) - Verification And Operational Audit
Prompt:
You are validating merged changes across both repos.

Objective:
- Prove behavior and log growth characteristics.

Scope:
- test execution and manual scenario checklist

Required work:
1. Run automated tests for client and server changes.
2. Execute manual scenarios:
- disconnect/reconnect during active updates
- server restart while client runs
- transient serial timeout burst
- transient sqlite lock at startup
3. Measure log growth for default mode and diagnostics mode under stress.
4. Produce a pass/fail table and residual risks.

Acceptance criteria:
- Recovery behavior confirmed for all configured dials.
- Logs contain actionable context in diagnostics mode.
- Default mode log growth remains controlled.

Deliverables:
- Verification report.
- Pass/fail matrix.
- Release readiness recommendation.

## Merge Gates
1. Gate 1: Phase A tests merged and green.
2. Gate 2: Client and server reliability branches rebased and green.
3. Gate 3: Logging controls merged and config compatibility validated.
4. Gate 4: Verification report signed off.

## Definition Of Done
1. Disconnect no longer causes persistent partial dial updates in validated scenarios.
2. Server does not silently lose queued updates after transient send failures.
3. Startup sqlite lock contention is retried with bounded strategy and clear errors.
4. Client diagnostics mode captures root-cause breadcrumbs.
5. Default logging remains low-noise with bounded disk usage.
