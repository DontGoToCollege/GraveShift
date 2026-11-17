using System.Windows.Threading;

namespace GraveShift.Core;

/// <summary>Works out the colour every tick, fades toward it and hands the ramp to Windows.</summary>
public sealed class Engine
{
    public const double MovieKelvin = 4600;
    public static readonly TimeSpan MovieLength = TimeSpan.FromMinutes(150);

    public Settings S { get; }
    readonly bool _applyToScreen;
    readonly DispatcherTimer _timer = new();

    public DateTime? DisabledUntil { get; private set; }
    public string? DisabledWhy { get; private set; }
    public DateTime? MovieUntil { get; private set; }
    public Effect Effect { get; private set; }
    public double? ManualKelvin { get; private set; }
    Phase _manualPhase;
    public double? PreviewKelvin { get; set; }

    public double CurrentKelvin { get; private set; } = 6500;
    double _currentBright = 1;
    public double TargetKelvin { get; private set; } = 6500;
    public Phase Phase { get; private set; }
    public string? LastApp { get; private set; }
    public string? PausedFor { get; private set; } // why it's switched off right now (app, full screen, timer)
    public bool Limited { get; private set; }      // Windows refused a ramp

    DateTime _lastApply;
    ushort[]? _lastRamp;

    public event Action? Changed;

    public Engine(Settings settings, bool applyToScreen)
    {
        S = settings;
        _applyToScreen = applyToScreen;
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        var (k, _) = Schedule.At(DateTime.Now, S);
        CurrentKelvin = k; // start already at the scheduled colour, no fade from white
        Tick();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_applyToScreen) Native.RestoreRamp();
    }

    /// <summary>Re-apply on the next tick (display change, wake from sleep).</summary>
    public void Refresh() { _lastRamp = null; Tick(); }

    public void DisableFor(TimeSpan span) { DisabledUntil = DateTime.Now + span; DisabledWhy = "timer"; Tick(); }
    public void DisableUntilSunrise() { DisabledUntil = Schedule.NextSunrise(DateTime.Now, S); DisabledWhy = "sunrise"; Tick(); }
    public void Enable() { DisabledUntil = null; DisabledWhy = null; Tick(); }
    public void ToggleHour() { if (DisabledUntil != null) Enable(); else DisableFor(TimeSpan.FromHours(1)); }

    public void SetEffect(Effect e) { Effect = e; Tick(); }
    public void ToggleMovie() { MovieUntil = MovieUntil == null ? DateTime.Now + MovieLength : null; Tick(); }

    public void SetManual(double? kelvin)
    {
        ManualKelvin = kelvin is double k ? Math.Clamp(k, 1200, 6500) : null;
        _manualPhase = Schedule.At(DateTime.Now, S).Phase;
        Tick();
    }

    public void NudgeColor(int step) => SetManual((ManualKelvin ?? TargetKelvin) + step);

    public void NudgeBrightness(double step)
    {
        S.Brightness = Math.Round(Math.Clamp(S.Brightness + step, 0.2, 1), 2);
        S.Save();
        Tick();
    }

    void Tick()
    {
        var now = DateTime.Now;
        if (DisabledUntil is DateTime until && now >= until) { DisabledUntil = null; DisabledWhy = null; }
        if (MovieUntil is DateTime m && now >= m) MovieUntil = null;

        var app = Native.ForegroundApp(out bool fullscreen);
        if (app != null) LastApp = app;

        var (scheduled, phase) = Schedule.At(now, S);
        Phase = phase;
        if (ManualKelvin != null && phase != _manualPhase) ManualKelvin = null; // manual colour lasts until the next change of phase

        PausedFor = DisabledUntil != null ? DisabledWhy
            : S.DisableFullscreen && fullscreen ? "full screen"
            : app != null && S.DisabledApps.Contains(app, StringComparer.OrdinalIgnoreCase) ? app
            : null;

        double k = PreviewKelvin ?? ManualKelvin ?? scheduled;
        if (MovieUntil != null) k = Math.Max(k, MovieKelvin);
        double bright = S.Brightness;
        var effect = Effect;
        if (PausedFor != null && PreviewKelvin == null) { k = 6500; bright = 1; effect = Effect.None; }
        TargetKelvin = k;

        // Jumps (sliders, disable, hotkeys) fade over about a second; the schedule already glides on its own.
        double fromM = 1e6 / CurrentKelvin, toM = 1e6 / k;
        bool fading = Math.Abs(toM - fromM) > 2 || Math.Abs(bright - _currentBright) > 0.005;
        CurrentKelvin = fading ? 1e6 / (fromM + (toM - fromM) * 0.15) : k;
        _currentBright = fading ? _currentBright + (bright - _currentBright) * 0.15 : bright;
        _timer.Interval = fading ? TimeSpan.FromMilliseconds(30) : TimeSpan.FromSeconds(1);

        var ramp = ScreenColor.Ramp(CurrentKelvin, _currentBright, effect);
        // Other programs and display changes can reset the ramp, so re-send it every few seconds anyway.
        if (_applyToScreen && (_lastRamp == null || !ramp.AsSpan().SequenceEqual(_lastRamp) || now - _lastApply > TimeSpan.FromSeconds(5)))
        {
            Limited = !Native.ApplyRamp(ramp);
            _lastRamp = ramp;
            _lastApply = now;
        }
        Changed?.Invoke();
    }
}
