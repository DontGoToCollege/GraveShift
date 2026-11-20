# GraveShift: notes for Claude

Build, test and release recipes, decisions and gotchas, kept in the repo so every computer has them.

GraveShift lives at `%USERPROFILE%\Music\Claude\Projects\GraveShift` (public repo DontGoToCollege/GraveShift). Started 2026-11-17; v1.0.1 is the first release (1.0.0 is archived only: garbled symbols, never released).

- Clean-room: f.lux 4.150 was NOT decompiled. REA (imports, resources) plus a strings dump only produced the feature list in `docs\FEATURES.md`. Ship no f.lux code, art, fonts or data. This PC has no REA deep provider (Ghidra/IDA not configured), so `list_strings` fails; use the PowerShell strings dump instead.
- Stack: WPF `net10.0-windows10.0.19041.0` (only .NET 10 SDKs on this PC) with `ThemeMode="Dark"` (Fluent), WinForms for the tray. Screen colour = `SetDeviceGammaRamp` per display; `GdiIcmGammaRange=256` is already set on this PC.
- Build: `.\publish.ps1 -Test` (test-build\, not archived), `.\publish.ps1` for a release. Smoke: copy the exe to a scratch folder, run `--smoke`: writes `GraveShift-Data\smoke.txt` + `smoke.png` (window rendered offscreen, never touches gamma).
- Gotchas: launching a second instance signals the first to show and activate its window, so never do that in tests (it steals focus). Running the real app tints the owner's screen, so only run it briefly in daytime (6500K = no visible change). Never round-trip source files through PowerShell Get-Content/Set-Content: PS 5.1 reads UTF-8 as ANSI and corrupts ☀ ☾ · −. Use the Edit tool.
- Owner switched over 2026-11-17: their real copy runs from `dist\GraveShift.exe --tray` (HKCU Run "GraveShift"), data in `dist\GraveShift-Data` — never delete it. publish.ps1 kills it, so relaunch it afterwards via WMI Win32_Process Create. f.lux's Run entry was removed (was `"...\FluxSoftware\Flux\flux.exe" /noshow`); f.lux itself is still installed. Windows location is off on this PC.
- Not built yet (owner may want later): Hue/smart lighting, webcam, backwards alarm, dim at night, sleep-in on weekends, dark mode at sunset.

**Why:** avoids re-deriving the clean-room boundary, build and test recipe.
**How to apply:** edit `src\GraveShift`, test-build, smoke, then bump `<Version>`, CHANGELOG, publish, push, `gh release create`. See user-testing-preference and portasnap-project.
