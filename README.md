# VR MOGGER

A Windows tray app that changes one selected desktop display's resolution and refresh rate when chosen games run, then restores it after a configurable delay.

**Kelly Label Studio × MK2XX2 · MIT licensed · Free beta**

## Beta status

Version **1.1.0-beta.1** adds monitor selection and restoration safeguards to the original prototype. This is a beta: automated tests and Windows builds do not replace testing with real monitors, SteamVR, and games. No FPS improvement or fixed memory footprint is promised. The desktop display is separate from the headset's render resolution.

## Use it

1. Close any older VR Mogger instance using its tray **Exit** menu.
2. Install the setup package, or extract the portable ZIP and run `VRMogger.exe`.
3. Choose the monitor to change. Other displays are left alone.
4. Add executable names such as `VRChat.exe` or `Beat Saber.exe`, or browse to a program.
5. Choose a supported **Mog Mode**. Leave **Restore the mode captured before gaming** enabled unless you want a custom desktop preset.
6. Save. The app runs near the Windows clock. Launch a listed game, then close all listed games to start the restoration countdown.

Use **Only trigger when SteamVR is running** if you want to avoid triggering for desktop-mode VRChat while SteamVR is closed. This checks SteamVR processes; it does not detect whether a headset is worn or which runtime a game actually uses.

**Ctrl+Alt+F9** toggles manual Mog Mode and desktop restoration. Manual Mog Mode stays active until restored. Manually restoring while games are running suppresses automatic switching until they all close. The tray also provides **Pause automation**, **Switch**, **Restore**, and **Settings**.

All trigger programs share one Mog Mode in this beta. Settings changed during a session apply to the next session; the current restoration target remains pinned to the display and mode captured when that session began.

## Requirements and installation

- Windows 11 desktop; .NET Framework 4.8 or newer (included with Windows 11).
- The selected monitor must expose the requested mode through its Windows display driver.
- No QRes, AutoHotkey, Node.js, Python, game injection, administrator elevation, account, or runtime downloads are required by the app.
- The installer installs for the current user under `%LocalAppData%\Programs\VRMogger`. Startup is opt-in from Settings.
- The portable package does not install the program, but settings are still stored in AppData.

Beta binaries are unsigned. Windows may show an unrecognized-publisher or reputation warning. A warning is not proof that a file is safe or malicious; verify the source and release checksums. Do not disable Windows protection to use this app.

## Restoration and recovery

Before a display change, VR Mogger saves the original width, height, and integer refresh rate to `%AppData%\VRMogger\recovery.ini`. If it cannot save recovery data, it does not change the mode. Normal Exit attempts restoration immediately. Failed restoration retains the record and retries, including when a disconnected monitor returns.

After a crash, forced termination, or power loss, restoration cannot run immediately. On the next launch, the app asks whether to restore the unfinished session or retain the current mode. If a saved monitor cannot be found, the app does not substitute the primary monitor.

The app uses monitor interface identities when available. Some virtual drivers only expose adapter identities; a driver reinstall or topology change can require selecting the monitor again. Width, height, and integer refresh are the scope of restoration; the app does not manage HDR, color profiles, desktop topology, or headset settings. Driver-reported fractional refresh rates may be rounded by the Win32 mode API.

Existing v1 `config.ini` game names, modes, delay, and startup preferences are read on upgrade. The monitor picker must be saved on first upgrade. Original-mode restoration is enabled by default; the old desktop preset is retained as an optional custom mode.

## Build and test

On Windows, open PowerShell in this repository and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The script uses the Windows .NET Framework C# compiler, runs the regression tests, and produces the executable and portable ZIP in `dist`. There are no NuGet dependencies. To also build the installer, install the official [NSIS tool](https://nsis.sourceforge.io/) on the build computer, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Installer
```

The GitHub Actions workflow performs the same Windows build and publishes packages, checksums, test output, and a generated settings-window preview as workflow artifacts. No release is automatically published. Hosted Windows runner tests do not establish Windows 11 hardware/VR compatibility; follow [the manual test guide](docs/TESTING.md) before promoting a beta.

## Support and contributions

This beta is free and open source. Feedback, reproducible bug reports, and contributions are welcome. Include your Windows version, GPU/driver, selected monitor, requested modes, game names, and whether SteamVR-only mode was enabled. Avoid posting private configuration or account details.

Optional donations can support development once an official donation destination is configured. No payment or donation account is created by installing the app.

## License

[MIT](LICENSE). You may use, modify, redistribute, and sell copies subject to the license notices. Open source does not grant rights to claim that modified builds are official Kelly Label Studio or MK2XX2 releases.
