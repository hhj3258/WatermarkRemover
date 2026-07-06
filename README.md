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

## "Unrecognized app" / SmartScreen warning

When you run the downloaded `.exe`, Windows may show a blue **"Windows protected your PC"** (SmartScreen) dialog, or your antivirus may flag it. To run it: click **More info → Run anyway**.

**Why this happens:**

- **The executable is not code-signed.** Publishing a signed app requires a paid code-signing certificate, which this personal project does not have. SmartScreen shows this warning for *any* unsigned app that hasn't built up download reputation yet — it is not a sign of malware by itself.
- **It requests administrator rights and stops system services.** Disabling `sppsvc`/`sppamsvc`/`svsvc` is exactly the kind of behavior antivirus heuristics watch for, so a false-positive flag is possible.
- **It was downloaded from the internet.** Files carry a "Mark of the Web" tag, which makes Windows extra cautious about unrecognized publishers.

If you would rather not trust the prebuilt binary, **[build it yourself from source](#build-from-source)** — the entire codebase is public in this repository.

---

## Tray Menu

Right-click the tray icon:

- **Status** — current blocking state and a live countdown to the next refresh
- **Unblock / Re-block watermark** — toggle
- **⚙ Settings**
  - **Refresh interval** — how often the watermark is re-checked (1 / 5 / 10 / 30 / 60 minutes)
  - **Language** — English / 한국어 (default: English)
  - **Run at Windows startup** — registers a scheduled task (runs elevated at logon). Enabled by default on first run, so boot → auto-launch → watermark blocked, hands-free.
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
