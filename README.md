# GraveShift

GraveShift warms your screen's colours as the sun goes down and goes back to normal in the
morning, so late nights are easier on your eyes and your sleep. It works completely offline,
needs no installing, and keeps its settings in a folder next to itself.

It's inspired by f.lux and does most of what f.lux does: daytime, sunset and bedtime colours,
presets, a wake time, smooth transitions, turning off for an hour, until sunrise, for full-screen
apps or for particular apps, hotkeys, Movie mode, Darkroom and colour effects. It's a fresh
program written from scratch: no f.lux code or artwork is included. See
[docs/FEATURES.md](docs/FEATURES.md) for the full comparison.

## Download

Get `GraveShift.exe` from the [Releases page](../../releases/latest). It's a single file for
64-bit Windows 10 or 11. Put it anywhere you like (a folder, a USB stick) and run it.

## Using it

1. Run `GraveShift.exe`. Its window opens and an icon appears in the tray (by the clock).
2. Open **Options** and set your location: type your latitude and longitude, or click
   **Ask Windows**. This makes sunrise and sunset right for where you live.
3. Pick a **Preset**, or drag the **Daytime**, **Sunset** and **Bedtime** sliders. While you
   drag, the screen shows that colour for a moment so you can see it.
4. Set your **Wake at** time. Bedtime colours start 8½ hours before it.
5. Close the window. GraveShift keeps running in the tray. Click the tray icon to open it again,
   or right-click it for **Turn off**, **Effects**, **Colour right now**, **Presets** and **Exit**.

If f.lux is also running, quit it first, or the two will fight over your screen.

### Hotkeys

| Keys | What they do |
|---|---|
| Alt+End | Turn off for an hour, or back on |
| Alt+PgUp / Alt+PgDn | Brighter / dimmer |
| Alt+Shift+PgUp / Alt+Shift+PgDn | Cooler / warmer until the next change |

### Very warm colours

Windows normally stops apps from making the screen warmer than about 3400K. If Options says the
full range is locked, click **Unlock** (Windows asks for permission) and restart the computer.

## Building it yourself

You need the .NET 10 SDK. Run `.\publish.ps1` to build `GraveShift.exe`, or
`GraveShift.exe --smoke` to run its self-test (results go to `GraveShift-Data\smoke.txt`).

## License

MIT: free for anyone to use, change and share. See [LICENSE](LICENSE).
