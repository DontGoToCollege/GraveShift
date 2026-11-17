# What f.lux does, and what GraveShift does

GraveShift is a clean-room program. f.lux 4.150 (`flux.exe`, SHA-256 `b42c47eb…f375c407`) was
**not** decompiled and none of its code, images, fonts or data ships with GraveShift. REA was used
only to list what the program does, from:

- its import table (REA `inspect_pe_imports`): how it changes the screen (GDI `SetDeviceGammaRamp`
  per display), hotkeys (`RegisterHotKey`), tray icon (`Shell_NotifyIcon`), idle and full-screen
  checks (`GetLastInputInfo`, `GetForegroundWindow`), monitor backlight (DDC/WMI) and networking
  (WinINet, for updates, Hue lights and location lookups);
- its resource table (REA `inspect_pe_resources`): icons, dialogs and a manifest only;
- the readable text inside the program and its `media\preset.json` / `runtime\flux.tre` files:
  menu wording, option names and the preset temperatures.

## Feature inventory

| f.lux feature | Seen as | GraveShift 1.0 |
|---|---|---|
| Warm the screen by time of day (daytime, sunset, bedtime colours) | "Choose a daytime/nighttime/bedtime color", presets with day/night/late | Yes |
| Sunrise/sunset from your location | "Change location...", latitude, "Where am I?" | Yes, offline: type latitude/longitude or ask Windows |
| Wake time sets bedtime | "Set your wakeup time", `waketime` | Yes (bedtime starts 8½ hours before wake time) |
| Presets | `preset.json` (7 presets of day/night/late Kelvin) | Yes, same temperatures under GraveShift's own names |
| Transition speed | "Very Fast: Gaming", "Medium: Adapt your eyes", "Slow: Natural Timing" | Yes |
| Disable for an hour / until sunrise / for full-screen apps / for the current app | Disable menu, Alt+End | Yes |
| Hotkeys | Alt+End, Alt+PgUp/PgDn (brightness), Alt+Shift+PgUp/PgDn (colour) | Yes |
| Change current colour (Ember 1200K … Sunlight 5500K) | colour list in the menu | Yes |
| Movie mode (2½ hours, gentler colour) | "Movie Mode" | Yes |
| Darkroom (inverted red) | "Darkroom mode" | Yes |
| Colour effects (Blue Sky, Emerald City, Salt Lamp, Soft White) | effect names | Yes, own tints |
| Wider colour range (below ~3400K needs a Windows setting) | `GdiIcmGammaRange` restart prompt | Yes, checks and offers to set it |
| Start at login | "Start f.lux at login" | Yes |
| Restores normal colours on exit | "Return to Windows default color settings?" | Yes |
| Philips Hue / YeeLight smart lighting | Hue bridge discovery, pairing | No (needs network) |
| Webcam room-colour detection | "Use a webcam to detect color in your room" | No |
| Backwards alarm clock, circadian graph, dark-mode-at-sunset, backlight dimming, vignette, updates, sharing settings | various | No (later, if wanted) |
