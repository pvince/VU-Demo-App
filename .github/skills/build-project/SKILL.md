---
name: build-project
description: "Build the VU1 Demo App WPF project. Use when: building debug, building release, running tests, publishing installer, dotnet build, dotnet test, NSIS installer, UIInvestigation configuration, checking SDK version."
argument-hint: "debug | release | test | publish | ui-investigation"
---

# Build VU1 Demo App

## Prerequisites

Before any build, verify the installed .NET SDK matches `global.json`:

```powershell
dotnet --version   # Should report 9.x
```

`global.json` pins `9.0.313` with `rollForward: latestFeature`. If the CLI reports a missing SDK error, update `global.json` to match the installed version or install the pinned SDK.

All commands below run from the **solution directory**:

```powershell
cd VU-Demo-App\VU1WPF
```

---

## Debug Build

Restore dependencies first if NuGet packages have changed, then build:

```powershell
dotnet restore
dotnet build --configuration Debug
```

Output: `bin\Debug\net8.0-windows\VU1-Demo-App.exe`

> The pre-build event automatically writes today's date to `Resources\BuildDate.txt`.

---

## Release Build

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
```

Output: `bin\Release\net8.0-windows\VU1-Demo-App.exe`

---

## UIInvestigation Build

Use this configuration to launch the app **without admin elevation** (uses `app.uiinvestigation.manifest` with `asInvoker`). Enables the `UI_INVESTIGATION` preprocessor constant.

```powershell
dotnet build --configuration UIInvestigation
```

Output: `bin\UIInvestigation\net8.0-windows\VU1-Demo-App.exe`

---

## Run Tests

Tests live in `VU1WPF.Tests\` (xunit). Run after a build with `--no-build`, or let dotnet test build automatically:

```powershell
# Run tests only (requires prior build):
dotnet test --no-build --verbosity normal

# Build and run in one step:
dotnet test --configuration Debug --verbosity normal
```

To run a single test class or method, use the `--filter` flag:

```powershell
dotnet test --filter "FullyQualifiedName~ClassDialGUITests"
```

---

## Publish Installer (NSIS)

### Step 1 — Release Build

```powershell
dotnet build --configuration Release --no-restore
```

### Step 2 — Create output folder

```powershell
New-Item -Path "..\" -Name "Artifacts" -ItemType Directory -Force
```

### Step 3 — Package with NSIS

Requires NSIS installed (`choco install nsis` or from https://nsis.sourceforge.io).

Run from the **repo root** (`VU-Demo-App\`):

```powershell
$repoRoot  = (Get-Location).Path   # must be VU-Demo-App\
$dist      = "$repoRoot\VU1WPF\bin\Release\net8.0-windows"
$output    = "$repoRoot\Artifacts\VU1-DemoApp-Installer.exe"

makensis /DINSTALLEROUTPUT="$output" /DDIRDIST="$dist" /DDIRSOURCE="$repoRoot" installer\install.nsi
```

Output: `Artifacts\VU1-DemoApp-Installer.exe`

---

## Quick Reference

| Goal | Command (from `VU1WPF\`) |
|------|--------------------------|
| Restore packages | `dotnet restore` |
| Debug build | `dotnet build -c Debug` |
| Release build | `dotnet build -c Release` |
| UIInvestigation build | `dotnet build -c UIInvestigation` |
| Run all tests | `dotnet test -c Debug` |
| Run tests (no rebuild) | `dotnet test --no-build` |
| Publish installer | See NSIS steps above (run from repo root) |

---

## Common Errors

| Error | Fix |
|-------|-----|
| `A compatible .NET SDK was not found` | Update `global.json` SDK version to match `dotnet --version` |
| Test files compiled into main build | Ensure `VU1WPF.csproj` has `<Compile Remove="VU1WPF.Tests\**\*.cs" />` |
| NSIS `makensis` not found | Install NSIS: `choco install nsis` |
| Admin elevation prompt missing | Use Release/Debug config; UIInvestigation config removes elevation |
