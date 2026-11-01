# Windows 11 beta test

Automated fake-display tests verify the session logic without changing real displays. A read-only Windows API probe verifies native structure sizes and mode enumeration. Neither test demonstrates actual switching with a laptop GPU, external monitor, or SteamVR.

Before a public release, test on a Windows 11 machine with the monitor modes already confirmed in Windows Settings. Keep Windows display settings accessible.

1. **Upgrade:** exit v1 normally; keep a copy of its `config.ini`. Start beta. Confirm game names, delay, and preferences remain. Save the monitor selection.
2. **Selected monitor:** choose the laptop, launch a trigger, and check the external monitor does not change. Repeat with the external monitor selected. In Windows duplicate/mirror mode, displays can be coupled by the OS; test extended desktop mode first.
3. **Restore:** close all triggers; check the exact configured delay and original resolution/refresh. Change the desktop mode between two sessions; confirm the new original is captured.
4. **Multiple games:** start two listed games, close one, and confirm no restore until both close. Restart during the restore delay; confirm the countdown is cancelled.
5. **Manual:** toggle with Ctrl+Alt+F9, restore while a trigger runs, and confirm it stays restored until all matching games close. Check manual mode without a game holds until restored. Check Pause and Resume.
6. **Exit:** quit from the tray while switched. Confirm immediate restoration. Reopen the app and verify no recovery prompt after a clean exit.
7. **Recovery:** in a controlled test, terminate the app through Task Manager while switched. Restart. Check both Restore and Keep Current choices in separate runs. Forced termination cannot restore immediately.
8. **Disconnect:** while an external display is selected, unplug it before restore, then reconnect. Confirm the laptop is never substituted and the saved restoration retries. A driver identity change should require reselecting the display.
9. **Custom desktop preset:** turn off captured-mode restoration, choose a custom desktop mode, and verify it after exit and game close. Then re-enable captured restoration.
10. **SteamVR gate:** with the checkbox on, run VRChat without SteamVR, then with SteamVR. This is a process check, not headset-wearing or runtime detection.
11. **Usability:** check Settings at 100%, 125%, 150%, and 200% display scaling, including moving it between different-DPI displays. Verify keyboard navigation, the clipped debug checkbox from v1 is resolved, and all buttons are reachable.
12. **Install/uninstall:** install without elevation, enable startup, sign out/in, and check one app instance starts. Exit, uninstall, and verify the shortcut/startup entry is removed while settings/recovery are retained. Check upgrade refuses to overwrite a running app.

## Resource and VR measurements

After startup settles, record Task Manager CPU and memory with Settings closed. Compare the same VR scene and graphics settings with the app idle and with a selected lower desktop mode. Record GPU/CPU frame times and headset refresh. Do not equate executable file size with RAM, or a lower desktop resolution with guaranteed headset FPS gains.

## Release record

Record Windows build, GPU and driver, monitor identities/modes, scaling factors, tested games/runtime, package checksum, and results. Keep the release labelled beta until real-device checks are complete. Never label an unsigned build antivirus-safe based only on source inspection or one scan.
