# context-mode — MANDATORY routing rules

## REDIRECTED — use sandbox

### Bash (>20 lines output)
Bash ONLY for: `git`, `mkdir`, `rm`, `mv`, `cd`, `ls`, `npm install`, `pip install`.
Otherwise: `mcp:context-mode:ctx_batch_execute(commands, queries)` or `mcp:context-mode:ctx_execute(language: "shell", code: ".")`

### Read (for analysis)
Reading to **Edit** → Read correct. Reading to **analyze/explore/summarize** → `mcp:context-mode:ctx_execute_file(path, language, code)`.

### Grep (large results)
Use `mcp:context-mode:ctx_execute(language: "shell", code: "grep ...")` in sandbox.

### Web (documentation, APIs)
Use `mcp:context-mode:ctx_fetch_and_index(url, source)` then `mcp:context-mode:ctx_search(queries)`.

### File listing
Use `mcp:context-mode:ctx_execute(language: "shell", code: "ls -la")` instead of Bash.

## Tool selection

1. **GATHER**: `mcp:context-mode:ctx_batch_execute(commands, queries)` — runs all commands, auto-indexes, returns search. ONE call replaces 30+. Each command: `{label: "header", command: "..."}`.
2. **FOLLOW-UP**: `mcp:context-mode:ctx_search(queries: ["q1", "q2", ...])` — all questions as array, ONE call.
3. **PROCESSING**: `mcp:context-mode:ctx_execute(language, code)` | `mcp:context-mode:ctx_execute_file(path, language, code)` — sandbox, only stdout enters context.
4. **WEB**: `mcp:context-mode:ctx_fetch_and_index(url, source)` then `mcp:context-mode:ctx_search(queries)` — raw HTML never enters context.
5. **INDEX**: `mcp:context-mode:ctx_index(content, source)` — store in FTS5 for later search.

## Output compression

- Terse like caveman. Technical substance exact.
- Drop articles, filler (just/really/basically), pleasantries, hedging.
- Fragments OK. Short synonyms. Code unchanged.
- Pattern: [thing] [action] [reason]. [next step].
- Auto-expand for security warnings, irreversible actions, user confusion.

## VU-Demo-App project rules

- Repo focus: Windows WPF client in `VU1WPF/`. Sibling `VU-Server/` repo is separate unless user asks for cross-repo work.
- Docs first: use `README.md` for setup, `ARCHITECTURE.md` for component map and config schema, and `.github/skills/build-project/SKILL.md` for build/test/publish flows.
- Default build and test working directory: `VU1WPF/`.
- Preserve established naming unless cleanup is requested explicitly. Legacy `Class*` types and mixed namespaces are normal here.
- `MetricPollingService` owns polling cadence and sensor status results.
- `ClassVUSensors` owns sensor lookup, exact identifier matching, and heuristic fallback.
- `DialComputationEngine` owns value scaling and threshold color resolution.
- `DialUpdateOrchestrator` owns non-blocking dial update delivery.
- `ClassConfigurationManager` and `DebouncedConfigSaver` own YAML persistence and save throttling.
- Never mask missing or invalid sensors by coercing them to `0`. Preserve explicit `SensorStatus` handling and graceful degradation.
- VU Server or HTTP failures must not freeze polling or crash the app. Prefer continued local operation with visible/logged failure.
- For behavior changes, add or update focused xUnit coverage in `VU1WPF/VU1WPF.Tests/` and prefer filtered `dotnet test` runs while iterating.
- For UI troubleshooting without elevation, use the `UIInvestigation` configuration instead of changing manifests ad hoc.
