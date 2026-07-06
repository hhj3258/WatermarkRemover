# WatermarkRemover — Project Overview

## 1. Summary

A Windows tray utility that removes the "Activate Windows" watermark by **disabling the software-protection services** and **hiding the watermark window in real time via a Win32 event hook**, running unattended from the system tray with a scheduled task for auto-start.

---

## 2. Tech Stack

| Area | Choice | Reason |
|------|--------|--------|
| UI framework | WinForms (.NET 8) | Lightweight tray app; `NotifyIcon` + `ContextMenuStrip` are native and dependency-free |
| Window hooking | `SetWinEventHook` (user32) | The watermark window is recreated on fullscreen transitions; polling can't keep up, so create/show events are hooked for millisecond-level response |
| Service control | `ServiceController` + `sc.exe` + registry | Stop and disable `sppsvc`/`sppamsvc`/`svsvc`; registry `Start=4` makes it persist across reboots |
| Settings storage | Registry `HKCU\SOFTWARE\WatermarkRemover` | No external config file; survives reinstalls and is trivial to read/write |
| Auto-start | Task Scheduler (`schtasks`) | A plain Run key cannot request elevation; a scheduled task runs the app elevated at logon |
| Rendering | Custom `ToolStripRenderer` | Dark theme + rounded corners (`DwmSetWindowAttribute`) that the default renderer can't produce |

---

## 3. File Structure

```
WatermarkRemover/
├── build.ps1                    # Publish a version folder + update the scheduled task
├── publish/
│   └── ver_*/                   # Per-version executables + CHANGES.md (change history)
└── WatermarkRemover/
    ├── Program.cs               # Entry point, single-instance mutex, auto-block-on-start
    ├── TrayApp.cs               # Tray icon, menu, live countdown, settings submenu
    ├── WatermarkBlocker.cs      # Core: service disabling + window hook + polling fallback
    ├── Settings.cs              # User settings (registry-backed)
    ├── AutoStartManager.cs      # Scheduled-task register/unregister
    ├── ModernMenuRenderer.cs    # Dark-theme menu renderer
    ├── Logger.cs                # Opt-in file logger (%LOCALAPPDATA%\WatermarkRemover\log.txt)
    ├── NativeMethods.cs         # Win32 P/Invoke declarations
    ├── Utils.cs                 # Validation helpers
    ├── app.manifest             # requestedExecutionLevel = requireAdministrator
    └── app.ico                  # Application icon (blue background, white W)
```

---

## 4. Execution Flow

```
Program.Main
  │
  ├─ single-instance mutex (exit if already running)
  ├─ if AutoEnableOnStart → force BlockingEnabled = true
  │
  ▼
TrayApp (ApplicationContext)
  │
  ├─ build tray icon + dark-theme context menu
  │
  └─ WatermarkBlocker.Start()
        │
        ├─ ApplyBlockingOnce()
        │     ├─ TryDisableProtectionServices()  (registry Start=4 + service Stop)
        │     ├─ if a service was stopped → RestartExplorer() + wait
        │     └─ TryHideWatermarkWindow()         (FindWindow "Worker Window" → SW_HIDE)
        │
        ├─ service-recheck timer (1h)   → ApplyBlockingOnce()   [Windows Update may re-enable services]
        ├─ watermark-refresh timer (5m) → TryHideWatermarkWindow() [polling fallback]
        │
        └─ InstallWindowEventHook()
              └─ SetWinEventHook(CREATE, SHOW)
                    └─ on event: if class == "Worker Window" → ShowWindow(SW_HIDE)
```

The tray menu recomputes its state (status text, countdown, checkmarks) on every `Opening`, and a 500 ms timer keeps the countdown ticking live while the menu is open.

---

## 5. Technical Challenges Solved

**1. Watermark reappearing after reboot**

Stopping the service once was not enough — Windows re-enabled it. Fixed by also writing registry `Start=4` (disabled) for all three protection services, plus an hourly recheck timer that re-applies the block in case Windows Update reverts it.

**2. Watermark flicker in fullscreen games**

When switching in and out of fullscreen (e.g. League of Legends), Windows recreates the `Worker Window`, and a 5-minute polling loop was far too slow to catch it — the watermark visibly flickered. Fixed with `SetWinEventHook` on `EVENT_OBJECT_CREATE`/`EVENT_OBJECT_SHOW`: the window is hidden within milliseconds of being recreated. Polling remains as a backup. Verified through the file log, which showed `hidden via WinEvent` entries firing exactly on each transition.

**3. Toggle state bug (behavior vs. intent)**

A method named `IsBlocking()` actually only reported whether the `sppsvc` service was stopped — not the user's blocking *intent*. During the "restart pending" state it returned the wrong value, so re-blocking showed an unblock dialog. Fixed by separating the two concepts: user intent lives in `Settings.BlockingEnabled`, and the service-state check was renamed to `IsServiceStopped()` to stop it from masquerading as an intent flag.

**4. Colored emoji in a custom-rendered menu**

The status rows use ✅/⏳/⚠ glyphs. Custom `ToolStripRenderer` draws text through GDI/GDI+, which renders emoji monochrome regardless of font. Multiple approaches (`TextRenderer`, `Segoe UI Emoji`, AntiAlias) were tried; colored emoji turned out to be a structural GDI limitation, so the glyphs are drawn in white and status color is conveyed by the row's background instead.

**5. White pixel line at the menu edge**

The dark rounded menu showed a 1–2 px white sliver at the top. Caused by the default `ToolStrip` border and the system default `BackColor`. Fixed by overriding `OnRenderToolStripBorder` to draw nothing, and setting the menu (and every submenu drop-down) `BackColor` to the dark theme color with zero padding.

---

## 6. Security & Design Notes

- **Local only** — the app talks to no external server. All actions are local service/registry/window operations.
- **No credentials, no telemetry** — nothing is collected or transmitted. The optional file log stays under `%LOCALAPPDATA%`.
- **Elevation is explicit** — admin rights are declared in the manifest and requested via UAC; the app does nothing silently in the background that isn't visible in the tray.
- **Does not alter activation** — only the visual watermark overlay is hidden; the Windows license state is never modified.

---

## 7. Deployment

`build.ps1` is the single entry point for releasing a version:

```powershell
.\build.ps1 -Version "1.0" -Changes "What changed|Why it changed"
```

It (1) publishes a framework-dependent single-file exe, (2) creates `publish/ver_<version>/` with the exe and a generated `CHANGES.md`, and (3) re-registers the Task Scheduler entry to point at the new version (elevated, via a UAC prompt).

GitHub releases attach the built `WatermarkRemover.exe` directly, so a second machine can download and run it without cloning — only the .NET 8 Desktop Runtime is required.

---

## 8. AI-Assisted Development

This project was built and iterated through AI-assisted sessions (Claude Code) with direct access to the build and runtime environment.

| Stage | Work | AI role |
|-------|------|---------|
| Research | Investigating how the watermark is drawn (Worker Window class, protection services) | Led — web/GitHub research + on-machine verification |
| Implementation | Full source (blocker, tray UI, settings, logger, renderer) | Wrote |
| Debugging | Reproducing the fullscreen flicker, reading the file log to confirm the WinEvent hook fired | Led — ran the app, inspected logs, iterated |
| Verification | Registry/service state checks, icon rendering checks, live tray inspection | Shared — AI executed, user judged the visual result |
| Conventions | A `coding-style` skill encoding naming, null-check, logging, and exception rules applied across every file | Enforced |

Representative cases:

- **Live behavior verification, not theory** — instead of assuming the event hook worked, the AI enabled the file log, triggered the watermark, and confirmed `hidden via WinEvent` entries were firing on each fullscreen transition.
- **Behavior-driven refactor** — a full pass renamed implementation-detail identifiers to behavior-based ones (`WatermarkKiller` → `WatermarkBlocker`, `_windowHideTimer` → `_watermarkRefreshTimer`), which also surfaced the `IsBlocking()` intent-vs-state bug.
- **Asset generation in-environment** — the application icon (multi-resolution `.ico`, blue background + white W) was generated programmatically to match the existing tray icon, rendered to a preview, and visually confirmed before embedding.
