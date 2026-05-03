---
description: "Run a VU1 Demo App release smoke check. Use for release build verification, test pass confirmation, NSIS packaging, and optional localhost VU Server sanity checks."
name: "Release Smoke Check"
argument-hint: "full | build-only | installer | with-server"
agent: "agent"
---

Run a VU1 Demo App release smoke check.

Use [build-project skill](../skills/build-project/SKILL.md), [README](../../README.md), and [ARCHITECTURE](../../ARCHITECTURE.md). If no argument is provided, treat the scope as `full`.

Recognized scopes:

- `build-only`: SDK check, restore if needed, Release build, artifact verification.
- `installer`: Release build plus NSIS packaging and installer artifact verification.
- `with-server`: `full` scope plus localhost VU Server contract sanity checks.
- `full`: SDK check, tests, Release build, release artifact verification, and installer build when NSIS is available.

Procedure:

1. Verify the installed .NET SDK against `global.json`.
2. Work from `VU1WPF/` for restore, build, and test steps unless the step explicitly requires repo root.
3. Run tests before release sign-off. Treat any failing xUnit test as blocking.
4. Build `Release` and verify `bin/Release/net8.0-windows/VU1-Demo-App.exe` exists.
5. If scope includes `installer`, or if scope is `full` and NSIS is available, package from repo root and verify `Artifacts/VU1-DemoApp-Installer.exe` exists.
6. If scope includes `with-server`, or if the change touched the HTTP contract, sanity check localhost connectivity against the expected `/api/v0/dial/list` and dial update endpoints with matching API key configuration.
7. Mark SDK mismatch, test failure, release build failure, or installer build failure as blocking. Mark missing NSIS or unavailable localhost server as `not run` unless the chosen scope requires that step.
8. Do not make unrelated code changes.

Return:

- Status table with step, result, and evidence.
- Blocking issues.
- Artifacts produced.
- Follow-up actions.