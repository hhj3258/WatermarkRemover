# WatermarkRemover

A system tray utility that removes the Windows activation watermark ("Activate Windows").
It stays in the system tray and automatically hides the watermark whenever it appears.

**Language:** English | [한국어](README.ko.md)

---

## Disclaimer

This project exists **purely for personal learning and study purposes**.
It does **not** bypass or crack Windows activation — it only hides the watermark overlay and never modifies the Windows license state.

If this repository is found to cause any problem or is requested to be taken down, **it will be deleted without hesitation**. Use it at your own risk.

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

`build.ps1` publishes a versioned build under `publish/ver_*/` and updates the scheduled task in one step.

---

## How It Works

Two strategies run in parallel: disabling the Windows software-protection services, and hiding the watermark window in real time via a Win32 event hook.

**See [ARCHITECTURE.md](ARCHITECTURE.md) for the detailed operating principles, project structure, and design notes.**
