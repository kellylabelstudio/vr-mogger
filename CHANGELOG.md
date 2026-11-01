# Changelog

## 1.1.0-beta.1

- Choose one connected desktop monitor; retain its identity and never substitute another display if it disconnects.
- Choose game and custom desktop modes from driver-reported lists.
- Capture the actual desktop mode before every session; retain the configurable custom desktop option.
- Save recovery information before switching. Restore on normal exit and offer recovery on next launch after an unfinished session.
- Retain failed restorations and retry with a delay; do not mark failed mode changes as successful.
- Keep manual restoration in effect until matching games close; add an automation pause control.
- Report settings, recovery, startup, and hotkey errors instead of silently claiming success.
- Preserve v1 settings on upgrade, with case-insensitive game deduplication.
- Add version metadata, an app icon, a per-user installer, portable packaging, and automated Windows regression tests.

## 1.0 prototype

The owner-tested single-file C# build supported primary-display switching, trigger programs, custom modes, restore delay, startup options, a tray menu, and Ctrl+Alt+F9. The exact uploaded source is preserved in the Git history before the beta implementation.
