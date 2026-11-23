# PowerModeSwitcher

A lightweight Windows system-tray utility that emulates laptop-style power modes (AFK/Macro, Casual, Performance) on desktop PCs by switching **Windows Power Plans** and **MSI Afterburner GPU profiles** simultaneously via global hotkeys or tray menu.

## Features
- **One-Click & Global Hotkey Switching:** Toggle modes instantly (`Ctrl + Alt + 1 / 2 / 3` by default).
- **Auto-Setup ("Create Plans & Profiles"):** Generates three tuned Windows power plans (`PMS - AFK / Macro`, `PMS - Casual`, `PMS - Performance`) and configures MSI Afterburner Power Limit profiles (`100%`, `60%`, `100%`).
- **Zero Bloat:** Native Win32 `powrprof.dll` and `RegisterHotKey` APIs with minimal RAM footprint.

## Quick Start
1. Download `PowerModeSwitcher.exe` from the **Releases** tab.
2. Run as **Administrator** if using the **Create Plans & Profiles** button so it can write to MSI Afterburner's `Program Files (x86)` folder.
3. Right-click the tray icon -> **Settings...** -> click **Create Plans & Profiles** -> **Save & Apply**.

## Build from Source
```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true