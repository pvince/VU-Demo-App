# VU1 Demo App

> **Note:** The changes in this fork are primarily made by AI, including local [Qwen](https://github.com/QwenLM/qwen) models and [GitHub Copilot](https://github.com/features/copilot) models.

A Windows desktop application that connects [VU Dials](https://vudials.com) hardware displays to real-time PC sensor data (CPU temperature, GPU load, memory usage, etc.) via a local VU Server instance.

## What is VU1 Demo App?

This project demonstrates how to integrate [VU Dials](https://vudials.com) hardware into a Windows desktop application. It consists of two components:

1. **VU-Server** (Python) — A Tornado-based HTTP server that communicates with VU Dial hardware via USB serial
2. **VU1-Demo-App** (.NET 8.0 WPF) — Reads PC sensor data and displays it on VU Dials in real time

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed technical documentation, component descriptions, and design patterns.

## What Changed in This Fork?

- **AI-driven development** — Core architecture and sensor resolution developed with AI assistance
- **Robust sensor resolution** — Heuristic fallback matching when hardware/drivers change
- **Metric polling abstraction** — Dedicated service with explicit status tracking (Available/Unavailable/Invalid)
- **Comprehensive test suite** — 53 passing xUnit tests covering all major functionality
- **Debounced config persistence** — Thread-safe async saving with 120ms debounce
- **Threshold color system** — Configurable backlight colors based on sensor values
- **Full sensor tree refresh** — Complete LibreHardwareMonitor traversal for consistent data

## Prerequisites

### VU-Server (Python)
- Python 3.8+
- USB serial access

### VU1-Demo-App (.NET)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (optional, for roll-forward compatibility)
- Windows 10/11 (WPF target)

## Quick Start

### Build & Run VU-Server

```bash
cd VU-Server
pip install -r requirements.txt
python server.py
```

The server will be available at `http://localhost:5340`.

### Build & Run VU1-Demo-App

```bash
cd VU-Demo-App/VU1WPF
dotnet restore
dotnet build
dotnet run
```

### Run Tests

```bash
cd VU-Demo-App/VU1WPF/VU1WPF.Tests
dotnet test  # All 53 tests should pass
```

### Build Installer

```bash
nsis /DINSTALLEROUTPUT="..\Artifacts\VU1-Installer.exe" /DDIRDIST="..\dist" /DDIRSOURCE=".." installer\install.nsi
```

## Usage

1. Start VU Server and connect VU Dials hardware via USB
2. Launch the Demo App (`VU1-Demo-App.exe` or `dotnet run`)
3. Use the UI to configure which sensors each dial displays
4. Set color thresholds for dial backlight
5. Monitor real-time sensor data on your dials

## Resources

- **[ARCHITECTURE.md](ARCHITECTURE.md)** — System design, components, configuration, and API details
- [VU Dials Home](https://vudials.com)
- [VU Dials API Docs](https://docs.vudials.com)
- [VU Server Repository](https://github.com/SasaKaranovic/VU-Server)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

## Contributing

Contributions welcome! Please submit pull requests or open issues. For integration details, see [ARCHITECTURE.md](ARCHITECTURE.md).

## License

See the original VU Dials licensing for details.
