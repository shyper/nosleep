# ⚡ NoSleep

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Build & Release](https://github.com/shyper/nosleep/actions/workflows/release.yml/badge.svg)](https://github.com/shyper/nosleep/actions/workflows/release.yml)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)](https://microsoft.com/windows)

**NoSleep** is a lightweight, portable Windows utility that automatically prevents your PC from entering standby / sleep mode during active downloads (Steam, Epic Games, EA App, web browsers) and disk activity (unpacking, game patching, installations), and seamlessly allows normal power-saving sleep when the computer is idle.

---

## 📥 Download

Grab the latest standalone executable from the **[GitHub Releases](https://github.com/shyper/nosleep/releases)** page:
- **`NoSleep.exe`**: Ready-to-run portable executable. No installation required.
- **`NoSleep-vX.X.X-Portable.zip`**: Complete package with executable and documentation.

---

## ✨ Features

- **⚡ Native Windows Standby Prevention**:
  - Leverages the Windows Win32 Power API (`SetThreadExecutionState`) to block sleep without noisy mouse-jitter hacks or synthetic keyboard events.
- **🌐 Network & 💾 Disk Throughput Monitoring**:
  - Live throughput counters measuring network download/upload rates and disk read/write throughput using standard Windows Performance Counters.
- **⏱️ Trigger Delay & Cooldown (Peak Filter & Grace Period)**:
  - **Trigger Delay**: High throughput must be sustained for a configurable duration (default: 5s) to avoid unnecessary activations from brief network spikes.
  - **Cooldown**: Retains sleep prevention for a grace period (default: 60s) after throughput drops to bridge download chunk pauses.
- **🔒 Keep PC Awake (Force Awake)**:
  - 1-click toggle to keep your computer awake indefinitely whenever needed.
- **📋 Activity Log with Full Clipboard Support**:
  - Double-click any log row or press `Ctrl+C` to copy log entries directly to your clipboard.
  - Right-click context menu with *Copy Selected*, *Copy All Logs*, and *Clear Log*.
- **🗕 Seamless Taskbar Minimization**:
  - Minimizes cleanly to the Windows Taskbar while maintaining 100% background monitoring.
  - Close button `[X]` provides customizable options (*Minimize to Taskbar*, *Exit Program*, or *Always Prompt*).
- **📦 100% Standalone & Portable**:
  - Zero external runtime dependencies. Runs on any modern Windows 10 or Windows 11 PC.

---

## ⚙️ Configuration

Settings can be customized directly in the graphical interface or via `config.json`:

| Setting | Default | Description |
| :--- | :--- | :--- |
| **Network Threshold** | `1.0 MB/s` | Minimum download throughput required to trigger sleep prevention. |
| **Disk Threshold** | `5.0 MB/s` | Minimum disk throughput required to trigger sleep prevention. |
| **Trigger Delay** | `5 sec` | Sustained activity time required before sleep is blocked (peak filter). |
| **Cooldown / Grace Period** | `60 sec` | Time sleep remains blocked after throughput drops below threshold. |
| **Monitor Network** | `Enabled` | Check download throughput for standby prevention. |
| **Monitor Disk** | `Enabled` | Check disk write/read throughput for standby prevention. |
| **Keep Display On** | `Disabled` | When disabled, displays can power down while the PC stays awake. |
| **Start with Windows** | `Disabled` | Automatically launch NoSleep on Windows boot. |
| **Start Minimized** | `Disabled` | Start minimized in taskbar on launch. |
| **Action on Close** | `Prompt` | Action when clicking `[X]` (*Prompt*, *Minimize to Taskbar*, *Exit*). |

---

## 🛠️ Building from Source

You can build `NoSleep.exe` locally using the included build scripts:

### Using PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

### Using Batch:
```cmd
build.bat
```

---

## 🚀 Automated GitHub Releases

This repository includes a pre-configured **GitHub Actions Workflow** (`.github/workflows/release.yml`) that automatically compiles and publishes releases.

### How to trigger a release:

1. **Via Git Tag**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
2. **Via GitHub UI**:
   - Go to the **Actions** tab in your repository.
   - Select **Build & Release NoSleep**.
   - Click **Run workflow** and enter your desired tag version (e.g. `v1.0.0`).

GitHub Actions will automatically:
- Compile `NoSleep.exe` on a fresh Windows runner.
- Package `NoSleep-v1.0.0-Portable.zip`.
- Calculate SHA256 checksums (`SHA256SUMS.txt`).
- Publish a new public release under **Releases** with downloadable assets.

---

## 📄 License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.
