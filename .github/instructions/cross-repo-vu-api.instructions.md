---
description: "Use when changing or debugging the VU Demo App <-> VU Server HTTP contract, endpoint paths, query parameters, API keys, dial UID handling, localhost connectivity, or /api/v0 behavior."
name: "Cross-Repo VU API"
applyTo:
  - "VU1WPF/ClassVUServer.cs"
  - "VU1WPF/DialUpdateOrchestrator.cs"
  - "VU1WPF/ClassConfigurationManager.cs"
  - "VU1WPF/MainWindow.xaml.cs"
---

# Cross-Repo VU API

- Shared cross-repo contract rules now live in the workspace canonical file: [Workspace Copilot Instructions](../../../.github/copilot-instructions.md).
- Keep this file focused on client-side touchpoints that may require synchronized server updates.
- Client request surface lives in `VU1WPF/ClassVUServer.cs` (`RefreshDialListAsync`, `UpdateDialValueAsync`, `UpdateDialBacklightAsync`, `UpdateDialNameAsync`, `UpdateDialBackgroundImageAsync`).
- Client update pacing and delivery flow live in `VU1WPF/DialUpdateOrchestrator.cs`; host, port, and API key wiring live in `VU1WPF/ClassConfigurationManager.cs`.
- If a route, query parameter, auth key behavior, or response shape changes under `/api/v0/`, coordinate matching server-side updates in `VU-Server/server.py`.
- Preserve client resilience behavior: server/API failures should be logged and must not crash polling.
- For design and build context, link out: [ARCHITECTURE](../../ARCHITECTURE.md), [build-project skill](../skills/build-project/SKILL.md), and [Workspace Copilot Instructions](../../../.github/copilot-instructions.md).