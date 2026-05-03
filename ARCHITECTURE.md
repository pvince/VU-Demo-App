# VU1 Demo App Architecture & Technical Reference

This document provides a comprehensive technical reference for the VU1 Demo App. It covers system architecture, component descriptions, design patterns, configuration details, and a detailed history of changes made in this fork.

**Last Updated:** 2026-05-02

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Key Components](#key-components)
3. [Major Changes in This Fork](#major-changes-in-this-fork)
4. [Configuration Reference](#configuration-reference)
5. [API Usage](#api-usage)
6. [Project Structure](#project-structure)
7. [Integration Guide](#integration-guide)

---

## System Architecture

The VU1 Demo App is a two-tier system that bridges PC hardware sensors with physical VU Dials hardware.

```
┌─────────────────────────────────────────────────────────────────┐
│  Windows Desktop Environment                                     │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  VU1-Demo-App (.NET 8.0 WPF)                             │   │
│  │  ┌──────────────────────────────────────────────────┐    │   │
│  │  │  MainWindow (UI Layer)                           │    │   │
│  │  │  • Configuration GUI                             │    │   │
│  │  │  • Real-time dial status display                │    │   │
│  │  │  • System tray integration                       │    │   │
│  │  └──────────────────────────────────────────────────┘    │   │
│  │                           ▲                                 │   │
│  │                           │                                 │   │
│  │  ┌──────────────────────────────────────────────────┐    │   │
│  │  │  Business Logic Layer                            │    │   │
│  │  ├──────────────────────────────────────────────────┤    │   │
│  │  │ ClassVUSensors        (Sensor abstraction)      │    │   │
│  │  │ MetricPollingService  (Polling loop)            │    │   │
│  │  │ DialComputationEngine (Value scaling & colors)  │    │   │
│  │  │ DialUpdateOrchestrator (Concurrent updates)     │    │   │
│  │  │ ClassConfigurationManager (Persistence)         │    │   │
│  │  └──────────────────────────────────────────────────┘    │   │
│  │                           ▲                                 │   │
│  │                           │                                 │   │
│  │  ┌────────────────────────┴──────────────────────────┐    │   │
│  │  │  Hardware Interfaces                              │    │   │
│  │  ├──────────────────────────────────────────────────┤    │   │
│  │  │ LibreHardwareMonitor (PC Sensors)               │    │   │
│  │  │ HTTP Client (VU Server REST API)                │    │   │
│  │  └──────────────────────────────────────────────────┘    │   │
│  └──────────────────────────────────────────────────────────┘    │
│                           ▲                                       │
└───────────────────────────┼───────────────────────────────────────┘
                            │ HTTP (localhost:5340)
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  VU-Server (Python/Tornado)                                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  REST API Endpoints                                      │   │
│  │  • /api/v0/dial/<UID>/set  (Update dial value)         │   │
│  │  • /api/v0/dial/list       (List connected dials)      │   │
│  │  • /api/v0/device/status   (Hardware status)           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                           ▲                                       │
│                           │ USB Serial                            │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Hardware Drivers                                        │   │
│  │  • dial_driver.py    (Dial protocol)                    │   │
│  │  • serial_driver.py  (USB communication)               │   │
│  └──────────────────────────────────────────────────────────┘   │
└───────────────────────────┬───────────────────────────────────────┘
                            │ USB Serial
                            ▼
                    ┌───────────────┐
                    │   VU Dials    │
                    │   (Hardware)  │
                    └───────────────┘
```

### Data Flow

1. **Metric Polling** — `MetricPollingService` periodically queries LibreHardwareMonitor for sensor values
2. **Value Computation** — `DialComputationEngine` scales raw sensor values to dial range (0-100) and determines threshold colors
3. **Concurrent Updates** — `DialUpdateOrchestrator` coordinates non-blocking HTTP requests to VU Server
4. **Hardware Control** — VU Server communicates with VU Dials hardware via USB serial

### Status Tracking

Each metric polling operation has an explicit status:

- **Available** — Sensor is accessible and returning valid numeric values
- **Unavailable** — Sensor not found or LibreHardwareMonitor cannot access it
- **Invalid** — Sensor returns null/NaN; no automatic fallback to zero

This explicit tracking prevents silent failures and makes the system more debuggable.

---

## Key Components

### MainWindow.xaml.cs
**Purpose:** Primary UI window with Material Design themes  
**Responsibilities:**
- Dialog configuration UI
- Real-time status display of connected dials
- System tray icon and run-on-startup integration
- Settings windows (SetColorWindow, ThresholdsWindow)

**Key Dependencies:** ClassConfigurationManager, DialUpdateOrchestrator, MetricPollingService

---

### ClassDialGUI.cs
**Purpose:** Data model representing a single VU Dial  
**Fields:**
- `UID` — Unique hardware identifier
- `FriendlyName` — User-facing display name
- `Metric` — Sensor metric to display (e.g., "temperature:CPU")
- `SensorIdentifier` — LibreHardwareMonitor sensor path
- `ScalingMin` / `ScalingMax` — Value range for linear scaling
- `Thresholds` — List of color thresholds with trigger values

**Serialization:** YAML-compatible (via YamlDotNet)

---

### MetricPollingService.cs
**Purpose:** Periodic sensor data collection with explicit status tracking  
**Key Methods:**
- `StartPolling()` — Begin polling loop (non-blocking)
- `StopPolling()` — Gracefully stop polling
- `GetMetricValue(metric)` — Retrieve current sensor value with status

**Status Handling:**
- Returns `Available` / `Unavailable` / `Invalid` enums instead of null
- Never silently converts null/NaN to zero
- Logs all sensor access attempts for debugging

**Thread-Safety:** Uses `ConcurrentDictionary` for metric storage

---

### ClassVUSensors.cs
**Purpose:** LibreHardwareMonitor abstraction with robust sensor resolution  
**Key Methods:**
- `GetSensorValue(identifier)` — Retrieve numeric value by sensor path
- `ResolveMetricBySensorName(name)` — Find sensor when identifier is stale
- `GetAvailableSensors()` — Return all accessible hardware sensors

**Fallback Strategy:**
When a saved sensor identifier becomes stale (e.g., after driver reinstall):
1. Attempts exact path match
2. Falls back to heuristic matching by sensor type and index
3. Returns `Unavailable` if fallback fails
4. Logs resolution attempts for troubleshooting

**Full Tree Refresh:**
Replaced partial hardware updates with complete LibreHardwareMonitor visitor traversal every poll cycle for consistency.

---

### DialComputationEngine.cs
**Purpose:** Value transformation and threshold color resolution  
**Key Methods:**
- `ScaleValue(rawValue, minBound, maxBound)` → decimal [0-100]
  - Linear min-max normalization
  - Clamps to [0, 100]
- `ResolveThresholdColor(value, thresholds)` → Color
  - Finds the highest threshold value ≤ current value
  - Returns associated backlight color

**Example Threshold Configuration:**
```yaml
thresholds:
  - value: 30
    color: "00FF00"  # Green
  - value: 70
    color: "FFFF00"  # Yellow
  - value: 90
    color: "FF0000"  # Red
```

---

### DialUpdateOrchestrator.cs
**Purpose:** Non-blocking concurrent HTTP dial updates  
**Key Methods:**
- `UpdateDials(metrics)` — Fire-and-forget HTTP POST requests
- `QueueUpdate(dial, value)` — Enqueue update with debouncing

**Concurrency:**
- Uses `Task.Run()` for non-blocking async HTTP
- Respects `dial_update_period` minimum interval between updates
- Gracefully handles server unreachability (logs warning, continues)

**HTTP Requests:**
```
POST http://localhost:5340/api/v0/dial/<UID>/set
Parameters:
  value  : 0-100 (dial position)
  key    : Master API key
```

---

### ClassConfigurationManager.cs
**Purpose:** YAML-based configuration persistence with debouncing  
**Configuration File:**
```
%USERPROFILE%\KaranovicResearch\VU1-DemoApp\vu1demo_config.yaml
```

**Key Methods:**
- `LoadConfiguration()` — Read YAML from disk
- `SaveConfiguration()` — Async save with 120ms debounce
- `GetDials()` → List<ClassDialGUI>

**Debounce Mechanism:**
- Throttles rapid saves to prevent excessive disk I/O
- Thread-safe via `SemaphoreSlim`
- Suitable for real-time UI updates (multiple config changes per second)

**YAML Schema:**
```yaml
server_url: "http://localhost:5340"
server_port: 5340
master_key: "your-api-key"
dial_update_period: 0.2  # seconds

dials:
  - friendly_name: "CPU Temp"
    uid: "dial-001"
    metric: "temperature:cpu"
    sensor_identifier: "/lpc/nct6798d/temperature/0"
    scaling_min: 30
    scaling_max: 100
    thresholds:
      - value: 60
        color: "00FF00"
      - value: 80
        color: "FFFF00"
      - value: 95
        color: "FF0000"
```

---

### ClassVUServer.cs
**Purpose:** HTTP client for VU Server REST API  
**Key Methods:**
- `UpdateDial(uid, value, key)` → Task
- `GetConnectedDials(key)` → List<string>
- `GetDeviceStatus(key)` → string

**Error Handling:**
- Catches `HttpRequestException` (server unreachable)
- Logs errors but does not throw (non-blocking)
- Uses reasonable timeouts (5 seconds default)

---

## Major Changes in This Fork

### 1. AI-Driven Development
**Status:** ✅ Implemented  
**Impact:** Core architecture, sensor resolution, and computation engine developed with:
- Local Qwen LLM models for design iteration
- GitHub Copilot for implementation assistance
- Human review and validation of all changes

**Benefit:** Accelerated development with modern architectural patterns from day one.

---

### 2. Sensor Resolution Robustness
**Status:** ✅ Implemented  
**Key Addition:** `ClassVUSensors.ResolveMetricBySensorName()` heuristic fallback

**Problem Solved:**
Before: After hardware driver updates, saved sensor identifiers became stale, causing dials to go unavailable.

After: Fallback strategy attempts to match sensors by type and relative position:
```csharp
// Exact match first
if (sensor.Identifier.ToString() == savedIdentifier) return sensor.Value;

// Fallback: match by type and index
var candidates = allSensors
    .Where(s => s.SensorType == expectedType && s.Index == expectedIndex)
    .ToList();
if (candidates.Count == 1) return candidates[0].Value;
```

**User Impact:** Dials remain functional across driver updates without reconfiguration.

---

### 3. Metric Polling Abstraction
**Status:** ✅ Implemented  
**Key Addition:** `MetricPollingService` with explicit status tracking

**Problem Solved:**
Before: Null/NaN sensor readings were silently converted to zero, masking hardware issues.

After: Three-state status system:
- **Available** → Valid numeric value
- **Unavailable** → Sensor not found
- **Invalid** → Null/NaN returned (explicit failure state)

**Implementation:**
```csharp
public enum SensorStatus { Available, Unavailable, Invalid }

public (SensorStatus status, decimal? value) GetMetricValue(string metric)
{
    if (!metrics.ContainsKey(metric))
        return (SensorStatus.Unavailable, null);
    
    var val = metrics[metric];
    if (decimal.IsNaN(val) || val == null)
        return (SensorStatus.Invalid, null);
    
    return (SensorStatus.Available, val);
}
```

**Benefit:** Clearer debugging; UI can display sensor unavailability explicitly.

---

### 4. Full Sensor Tree Refresh
**Status:** ✅ Implemented  
**Key Change:** Replaced partial LibreHardwareMonitor updates with complete traversal

**Problem Solved:**
Before: Incremental updates could miss newly available sensors (e.g., after USB device reconnection).

After: Every poll cycle:
1. Completely rebuild sensor list from LibreHardwareMonitor
2. Visit all hardware components recursively
3. Update all metric values in one pass

**Code Pattern:**
```csharp
public void RefreshAllSensors()
{
    var computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, ... };
    computer.Open();
    
    var sensors = new Dictionary<string, decimal>();
    foreach (var hw in computer.Hardware)
    {
        VisitHardware(hw, sensors);
    }
    
    metrics.Clear();
    foreach (var kv in sensors) 
        metrics.TryAdd(kv.Key, kv.Value);
}

void VisitHardware(IHardware hw, Dictionary<string, decimal> sensors)
{
    foreach (var sensor in hw.Sensors)
        sensors[sensor.Identifier.ToString()] = (decimal)sensor.Value;
    
    foreach (var subhw in hw.SubHardware)
        VisitHardware(subhw, sensors);
}
```

**Performance:** Negligible (complete traversal takes ~100ms on typical hardware).

---

### 5. Comprehensive Test Suite
**Status:** ✅ Implemented  
**Test Coverage:** 53 passing xUnit tests

**Test Categories:**
- **Configuration Tests** (7)  
  - YAML loading/saving
  - Debounce mechanism
  - File I/O error handling

- **Sensor Resolution Tests** (9)  
  - Exact match resolution
  - Fallback heuristics
  - Unavailable sensor handling

- **Scaling Validation Tests** (8)  
  - Linear normalization (0-100)
  - Boundary conditions
  - Invalid input handling

- **Metric Polling Tests** (12)  
  - Status tracking (Available/Unavailable/Invalid)
  - Null/NaN handling
  - Concurrent access

- **Dial Computation Tests** (10)  
  - Threshold color resolution
  - Edge cases (min/max values)

- **End-to-End Regression Tests** (7)  
  - Full polling loop
  - Configuration persistence
  - Concurrent dial updates

**Test Location:** `VU1WPF.Tests/` directory  
**Running Tests:**
```bash
cd VU1WPF.Tests
dotnet test
```

---

### 6. Debounced Config Persistence
**Status:** ✅ Implemented  
**Key Component:** `DebouncedConfigSaver.cs`

**Motivation:**
Real-time UI updates can trigger many configuration changes per second. Without debouncing, this causes excessive disk I/O and potential file contention.

**Implementation:**
```csharp
public class DebouncedConfigSaver
{
    private Timer _debounceTimer;
    
    public void RequestSave()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ => SaveToDisk(),
            null,
            TimeSpan.FromMilliseconds(120),  // Debounce window
            Timeout.Infinite
        );
    }
}
```

**Configuration:**
```yaml
# In config.yaml
debounce_ms: 120  # Adjustable (default: 120ms)
```

**Benefit:** Smooth UI responsiveness without file system overhead.

---

### 7. Threshold Color System
**Status:** ✅ Implemented  
**Key Feature:** Dial backlight colors based on sensor value ranges

**UI Windows:**
- **SetColorWindow.xaml** — Per-threshold color picker (Material Design)
- **ThresholdsWindow.xaml** — Manage multiple thresholds per dial

**Configuration Schema:**
```yaml
thresholds:
  - value: 30    # When sensor ≤ 30, use this color
    color: "00FF00"  # Green (hex without #)
  - value: 70
    color: "FFFF00"  # Yellow
  - value: 90
    color: "FF0000"  # Red
```

**Resolution Logic:**
```csharp
public Color ResolveThresholdColor(decimal value, List<Threshold> thresholds)
{
    var applicableThreshold = thresholds
        .Where(t => value >= t.Value)
        .OrderByDescending(t => t.Value)
        .FirstOrDefault();
    
    return applicableThreshold?.Color ?? Color.White;
}
```

---

## Configuration Reference

### Configuration File Location
```
%USERPROFILE%\KaranovicResearch\VU1-DemoApp\vu1demo_config.yaml
```

Typical paths:
- `C:\Users\YourUsername\KaranovicResearch\VU1-DemoApp\vu1demo_config.yaml` (Windows)

### Complete Configuration Schema

```yaml
# VU Server Connection
server_url: "http://localhost"
server_port: 5340
master_key: "your-api-key-here"

# Polling & Update Behavior
metric_polling_interval: 1.0  # seconds
dial_update_period: 0.2       # minimum seconds between dial updates
debounce_ms: 120              # debounce window for config saves

# Dial Configurations
dials:
  - friendly_name: "CPU Temperature"
    uid: "dial-001"
    metric: "temperature:cpu"
    sensor_identifier: "/lpc/nct6798d/temperature/0"
    scaling_min: 30
    scaling_max: 100
    thresholds:
      - value: 50
        color: "00FF00"  # Green
      - value: 75
        color: "FFFF00"  # Yellow
      - value: 90
        color: "FF0000"  # Red

  - friendly_name: "GPU Load"
    uid: "dial-002"
    metric: "load:gpu"
    sensor_identifier: "/gpu/0/load/0"
    scaling_min: 0
    scaling_max: 100
    thresholds:
      - value: 50
        color: "00FF00"
      - value: 80
        color: "FFFF00"
```

### Configuration Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `server_url` | string | Yes | VU Server hostname/IP |
| `server_port` | int | Yes | VU Server port (usually 5340) |
| `master_key` | string | Yes | API authentication key |
| `metric_polling_interval` | float | No | Seconds between sensor polls (default: 1.0) |
| `dial_update_period` | float | No | Min seconds between dial updates (default: 0.2) |
| `debounce_ms` | int | No | Config save debounce window (default: 120ms) |
| `dials` | array | Yes | List of dial configurations |

### Dial Configuration Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `friendly_name` | string | Yes | Display name in UI |
| `uid` | string | Yes | Hardware dial UID (from VU Server) |
| `metric` | string | Yes | Sensor metric identifier |
| `sensor_identifier` | string | Yes | LibreHardwareMonitor sensor path |
| `scaling_min` | int | Yes | Minimum value for linear scaling |
| `scaling_max` | int | Yes | Maximum value for linear scaling |
| `thresholds` | array | Yes | Color threshold definitions |

### Threshold Configuration Fields

| Field | Type | Description |
|-------|------|-------------|
| `value` | int | Sensor value threshold (≥ triggers this color) |
| `color` | string | Hex color code without `#` (e.g., "FF0000") |

---

## API Usage

### VU Server REST API

The VU1 Demo App communicates with VU Server via these endpoints:

#### Update Dial Value

```http
POST http://localhost:5340/api/v0/dial/<UID>/set?value=<VALUE>&key=<API_KEY>
```

**Parameters:**
- `UID` — Dial unique identifier (from config)
- `VALUE` — Dial position (0-100)
- `API_KEY` — Master API key

**Example:**
```bash
curl "http://localhost:5340/api/v0/dial/dial-001/set?value=75&key=your-api-key"
```

**Response:**
```
200 OK - Dial updated successfully
500 Server Error - Check server logs
```

---

#### List Connected Dials

```http
GET http://localhost:5340/api/v0/dial/list?key=<API_KEY>
```

**Response:**
```json
[
  {
    "uid": "dial-001",
    "name": "Primary Dial",
    "connected": true
  }
]
```

---

#### Get Device Status

```http
GET http://localhost:5340/api/v0/device/status?key=<API_KEY>
```

**Response:**
```json
{
  "status": "online",
  "dials_connected": 2,
  "uptime": 3600
}
```

For complete API documentation, see [VU Dials API Docs](https://docs.vudials.com/api_messaging/).

---

## Project Structure

```
VU-Demo-App/
│
├── VU-Server/
│   ├── server.py                # Main Tornado application
│   ├── dial_driver.py           # Dial hardware communication protocol
│   ├── serial_driver.py         # USB serial port abstraction
│   ├── server_config.py         # Configuration loading
│   ├── database.py              # API key persistence
│   ├── vu_notifications.py      # Hardware event notifications
│   ├── requirements.txt         # Python dependencies
│   ├── config.yaml              # Server configuration
│   ├── assets/                  # Static assets
│   ├── dials/                   # Hardware-specific drivers
│   ├── installer/               # NSIS installer scripts
│   └── www/                     # Web dashboard UI
│
├── VU-Demo-App/
│   ├── VU1WPF/
│   │   ├── MainWindow.xaml              # Primary UI window
│   │   ├── MainWindow.xaml.cs           # UI logic
│   │   ├── AboutWindow.xaml             # About dialog
│   │   ├── SetColorWindow.xaml          # Color picker UI
│   │   ├── ThresholdsWindow.xaml        # Threshold manager UI
│   │   ├── ClassDialGUI.cs              # Dial data model
│   │   ├── ClassVUServer.cs             # VU Server HTTP client
│   │   ├── ClassVUSensors.cs            # LibreHardwareMonitor abstraction
│   │   ├── ClassConfigurationManager.cs # YAML config persistence
│   │   ├── MetricPollingService.cs      # Sensor polling service
│   │   ├── DialComputationEngine.cs     # Scaling & color logic
│   │   ├── DialUpdateOrchestrator.cs    # Concurrent dial updates
│   │   ├── DebouncedConfigSaver.cs      # Debounced config saves
│   │   ├── VU1WPF.csproj                # Project file (.NET 8.0)
│   │   ├── VU1WPF.sln                   # Solution file
│   │   ├── App.xaml                     # Application resources
│   │   ├── App.xaml.cs                  # Application entry point
│   │   ├── AssemblyInfo.cs              # Version info
│   │   ├── Properties/                  # Localized resources
│   │   ├── Resources/                   # App resources (icons, etc.)
│   │   ├── bin/                         # Build output
│   │   │   ├── Debug/net6.0-windows/    # .NET 6.0 debug build
│   │   │   ├── Debug/net8.0-windows/    # .NET 8.0 debug build
│   │   │   └── Release/net8.0-windows/  # .NET 8.0 release build
│   │   ├── obj/                         # Intermediate build files
│   │   │
│   │   └── VU1WPF.Tests/
│   │       ├── ClassDialGUITests.cs             # Dial model tests
│   │       ├── ConfigurationManagerTests.cs    # Config persistence tests
│   │       ├── MetricPollingServiceTests.cs    # Polling service tests
│   │       ├── DialComputationTests.cs         # Scaling & color tests
│   │       ├── SensorResolutionTests.cs        # Sensor matching tests
│   │       ├── ScalingValidationTests.cs       # Value normalization tests
│   │       ├── EndToEndRegressionTests.cs      # Integration tests
│   │       ├── VU1WPF.Tests.csproj             # Test project file
│   │       └── bin/Release/                    # Test build output
│   │
│   ├── docs/
│   │   └── temperature-metric-investigation-and-tdd-plan.md  # Design docs
│   │
│   ├── installer/
│   │   ├── install.nsi        # NSIS installer script
│   │   └── inc/               # NSIS includes
│   │
│   ├── global.json            # .NET SDK version constraint
│   └── README.md              # Quick start guide
│
└── ARCHITECTURE.md            # This file
```

---

## Integration Guide

### For Third-Party Applications

Any application can integrate VU Dials by making HTTP requests to VU Server:

```csharp
// Example: C# WinForms or Console app
using System.Net.Http;

async Task UpdateDial(string uid, int value)
{
    var client = new HttpClient();
    var url = $"http://localhost:5340/api/v0/dial/{uid}/set?value={value}&key=YOUR_API_KEY";
    var response = await client.GetAsync(url);
    
    if (response.IsSuccessStatusCode)
        Console.WriteLine("Dial updated");
    else
        Console.WriteLine($"Error: {response.StatusCode}");
}
```

### For Developers Contributing to VU1-Demo-App

#### Adding a New Sensor/Metric

1. **Identify LibreHardwareMonitor Path**
   - Run the app, open Thresholds dialog
   - Note the sensor identifier (e.g., `/lpc/nct6798d/temperature/0`)

2. **Create New Dial Configuration**
   - Edit `vu1demo_config.yaml`
   - Add dial entry with sensor identifier

3. **Configure Scaling & Thresholds**
   - Set `scaling_min` and `scaling_max` based on sensor range
   - Define thresholds with colors

4. **Test**
   - Restart app and monitor dial in real-time

#### Extending Sensor Resolution

To add additional fallback strategies in `ClassVUSensors`:

```csharp
public decimal? ResolveMetricBySensorName(string sensorName)
{
    // 1. Try exact match (existing)
    if (exactMatch) return value;
    
    // 2. Try heuristic by type/index (existing)
    if (heuristicMatch) return value;
    
    // 3. Add custom fallback here
    if (customStrategy) return value;
    
    return null; // Unavailable
}
```

#### Running Tests Locally

```bash
cd VU-Demo-App/VU1WPF/VU1WPF.Tests
dotnet test --logger:console --verbosity:normal
```

For CI/CD integration, all 53 tests should pass.

---

## Development Guidelines

### Code Style
- Follow Microsoft C# naming conventions (PascalCase for classes/methods, camelCase for locals)
- Use `readonly` for immutable fields
- Prefer explicit status returns over null/exceptions

### Async Patterns
- Use `Task`/`async await` for I/O operations
- Never block on `.Result`
- Handle `TaskCanceledException` in polling loops

### Configuration Changes
- Always update YAML schema documentation
- Add corresponding xUnit tests
- Test debounce behavior under rapid changes

### Sensor Addition
- Always use full LibreHardwareMonitor identifier path
- Add test case for sensor resolution fallback
- Document new sensor in config reference

---

## FAQ & Troubleshooting

### Dials Show as "Unavailable"

**Causes:**
1. VU Server not running (`http://localhost:5340` unreachable)
2. Sensor identifier stale (hardware driver changed)
3. LibreHardwareMonitor cannot access sensor

**Steps:**
1. Check VU Server is running: `curl http://localhost:5340/api/v0/device/status?key=YOUR_KEY`
2. Review app logs for `Unavailable` status messages
3. Use sensor resolution fallback: Delete `sensor_identifier` in config and re-scan

### High CPU Usage

**Causes:**
- `metric_polling_interval` too low (default: 1.0s)
- Too many LibreHardwareMonitor hardware monitors enabled

**Fix:**
- Increase `metric_polling_interval` to 2.0 or higher
- Disable unused hardware monitors in LibreHardwareMonitor settings

### Config Changes Not Persisting

**Causes:**
- File permissions issue on `%USERPROFILE%\KaranovicResearch\`
- Antivirus blocking writes

**Fix:**
- Check folder permissions (should be user-writable)
- Check antivirus logs for blocked file access

---

## Version History

**Current Version:** 1.0.0 (May 2026)

### Major Milestones
- **v1.0.0** — Initial AI-assisted fork with sensor resolution, metric polling, debounced config, and 53-test suite
- **v0.9.0** — Community fork baseline (sensor scaling, threshold colors)

---

## License & Attribution

This project is a fork of the original [VU-Server](https://github.com/SasaKaranovic/VU-Server) ecosystem.

**Original Project:** [SasaKaranovic/VU-Server](https://github.com/SasaKaranovic/VU-Server)  
**Original Author:** Sasa Karanovic  
**Fork Modifications:** AI-assisted development (Qwen + GitHub Copilot), with significant additions to sensor resolution, testing, and configuration architecture.

---

**For questions or contributions, refer to the main [README.md](README.md).**
