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

- Use this instruction for changes that can break the VU1WPF client to VU-Server contract. Inspect both repos before editing one side only.
- Current contract is hard-coded under `/api/v0/`. If a route, method, query name, or response shape changes, update both repos together or preserve backward compatibility.
- Client request surface lives primarily in `VU1WPF/ClassVUServer.cs`; delivery and pacing live in `VU1WPF/DialUpdateOrchestrator.cs`; host, port, and API key wiring live in `VU1WPF/ClassConfigurationManager.cs`.
- Server route surface lives primarily in `server.py`; dial execution and queued state updates live in `server_dial_handler.py`; auth and config wiring live in `server_config.py` and `database.py`.
- Preserve method and parameter compatibility unless the task is an intentional contract revision. Current app behavior depends on GET `/api/v0/dial/list`, GET `/api/v0/dial/{uid}/set`, GET `/api/v0/dial/{uid}/backlight`, GET `/api/v0/dial/{uid}/name`, and POST `/api/v0/dial/{uid}/image/set`.
- Keep auth parameters synchronized. The client sends `key` on list, set, backlight, and image requests. Rename uses `admin_key` on the server path. If auth names or requirements change, update both sides and verification steps together.
- Keep dial UIDs opaque strings. Do not normalize, parse, or reformat them on one side only.
- Keep dial values and backlight channels on the 0-100 percentage scale across the wire. Do not switch one side to 0-255 raw values without coordinated changes.
- Preserve current failure behavior unless the task is explicitly about reliability changes: the client logs and returns failure without crashing the poll loop, and the server returns JSON fail payloads with HTTP status codes.
- After changing the contract, run a localhost sanity check with matching host, port, and API key configuration. Verify list plus at least one dial update path.
- Link to existing docs instead of restating them: [ARCHITECTURE](../../ARCHITECTURE.md) for client design and config schema, [build-project skill](../skills/build-project/SKILL.md) for app-side build and test flow.