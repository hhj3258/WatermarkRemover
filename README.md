# WatermarkRemover

A system tray utility that removes the Windows activation watermark ("Activate Windows").
It stays in the system tray and automatically hides the watermark whenever it appears.

**Language:** English | [한국어](README.ko.md)

---

## Requirements

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — must be installed before running
- **Administrator privileges** — required for service control (declared in the manifest, so it is requested automatically on launch)

---

## Download & Run

1. Download `WatermarkRemover.exe` from the [latest release](../../releases/latest)
2. Run it (the UAC prompt appears automatically)
3. A blue **W** icon appears in the system tray
4. Right-click the tray icon → **⚙ Settings** → check **Run at Windows startup**

> The watermark disappears within a few seconds. It is hidden again automatically even after switching in and out of fullscreen games.

---

## Tray Menu

Right-click the tray icon:

- **Status** — current blocking state and a live countdown to the next refresh
- **Unblock / Re-block watermark** — toggle
- **⚙ Settings**
  - **Refresh interval** — how often the watermark is re-checked (1 / 5 / 10 / 30 / 60 minutes)
  - **Run at Windows startup** — registers a scheduled task (runs elevated at logon)
  - **Auto-block on start** — always start in the blocking state after boot
  - **Write action log to file** — logs to `%LOCALAPPDATA%\WatermarkRemover\log.txt` (for debugging)
- **Exit**

---

## Build from Source

```powershell
dotnet publish WatermarkRemover/WatermarkRemover.csproj -c Release -r win-x64 --self-contained false
```

Output: `WatermarkRemover/bin/Release/net8.0-windows/win-x64/publish/WatermarkRemover.exe`

Versioned builds live under `publish/ver_*/`, each with a `CHANGES.md` describing what changed. The `build.ps1` script publishes a new version folder and updates the scheduled task in one step:

```powershell
.\build.ps1 -Version "1.1" -Changes "Change description|Reason for the change"
```

---

## File Structure

```
WatermarkRemover/
├── build.ps1                    # Version-publish + Task Scheduler update script
├── publish/
│   └── ver_*/                   # Per-version executables + CHANGES.md
└── WatermarkRemover/
    ├── Program.cs               # Entry point, single-instance guard
    ├── TrayApp.cs               # Tray icon and menu (UI)
    ├── WatermarkBlocker.cs      # Core blocking logic (service control + window hook)
    ├── Settings.cs              # User settings (registry HKCU\SOFTWARE\WatermarkRemover)
    ├── AutoStartManager.cs      # Task Scheduler based auto-start
    ├── ModernMenuRenderer.cs    # Dark-theme menu renderer
    ├── Logger.cs                # File logger
    ├── NativeMethods.cs         # Win32 P/Invoke
    ├── Utils.cs                 # Validation helpers
    └── app.ico                  # Application icon (blue background, white W)
```

---

## How It Works

Two strategies run in parallel.

### 1. Disabling protection services
The `sppsvc` / `sppamsvc` / `svsvc` services are stopped and set to disabled, which prevents the watermark from being rendered in the first place. This requires administrator privileges.

### 2. Real-time watermark hiding
Windows draws the watermark inside a window of class `Worker Window`. The app watches for it and hides it the moment it appears.

- **Event hook** — `SetWinEventHook` subscribes to window create/show events. When a `Worker Window` is (re)created — for example after switching in and out of a fullscreen game — it is hidden within a few milliseconds.
- **Polling fallback** — a periodic timer (default 5 minutes, adjustable in Settings) also re-hides the window, in case the hook ever misses an event.

### Auto-start
"Run at Windows startup" registers a Task Scheduler task that runs the app **elevated at logon** (a plain registry Run key cannot request elevation, so a scheduled task is used instead).

---

## Notes

This tool is for personal, educational, and convenience purposes. It does **not** bypass Windows activation — it only hides the watermark overlay and does not change the license state itself.
