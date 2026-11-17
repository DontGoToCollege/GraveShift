using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GraveShift.Core;
using Microsoft.Win32;

namespace GraveShift;

public partial class MainWindow : Window
{
    readonly Engine _engine;
    readonly DispatcherTimer _previewEnd = new() { Interval = TimeSpan.FromSeconds(1.5) };
    bool _loading;
    DateTime _graphDrawn;

    Settings S => _engine.S;

    public MainWindow(Engine engine)
    {
        _engine = engine;
        InitializeComponent();
        Title = $"GraveShift {App.Version}";

        PresetBox.ItemsSource = Presets.All.Select(p => p.Name).Append("Custom").ToList();
        PresetBox.SelectionChanged += (_, _) =>
        {
            if (_loading || PresetBox.SelectedItem is not string name) return;
            if (Presets.All.FirstOrDefault(p => p.Name == name) is Preset p) ((App)Application.Current).ApplyPreset(p);
        };

        HookSlider(DaySlider, v => S.Day = v);
        HookSlider(NightSlider, v => S.Night = v);
        HookSlider(LateSlider, v => S.Late = v);
        _previewEnd.Tick += (_, _) => { _previewEnd.Stop(); _engine.PreviewKelvin = null; _engine.Refresh(); };

        WakeDown.Click += (_, _) => ShiftWake(-15);
        WakeUp.Click += (_, _) => ShiftWake(15);

        SpeedBox.SelectionChanged += (_, _) =>
        {
            if (_loading || SpeedBox.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
            S.Speed = tag; S.Save(); Redraw();
        };
        StartupBox.Click += (_, _) => { S.StartWithWindows = StartupBox.IsChecked == true; S.Save(); App.SetStartup(S.StartWithWindows); };
        FullscreenBox.Click += (_, _) => { S.DisableFullscreen = FullscreenBox.IsChecked == true; S.Save(); _engine.Refresh(); };
        SaveLocation.Click += (_, _) => SaveTypedLocation();
        FindMe.Click += async (_, _) => await AskWindowsForLocation();
        RangeButton.Click += (_, _) => UnlockRange();

        Graph.SizeChanged += (_, _) => Redraw();
        _engine.Changed += OnEngineChanged;
        Closing += (_, e) => { e.Cancel = true; Hide(); }; // the app lives in the tray; Exit is in the tray menu
        IsVisibleChanged += (_, _) => { if (IsVisible) { LoadFromSettings(); OnEngineChanged(); } };
        LoadFromSettings();
        OnEngineChanged();
    }

    /// <summary>Puts the saved settings into every control.</summary>
    public void LoadFromSettings()
    {
        _loading = true;
        DaySlider.Value = S.Day; NightSlider.Value = S.Night; LateSlider.Value = S.Late;
        PresetBox.SelectedItem = Presets.All.Any(p => p.Name == S.Preset) ? S.Preset : "Custom";
        PresetInfo.Text = Presets.All.FirstOrDefault(p => p.Name == S.Preset)?.Description ?? "Your own colours";
        WakeText.Text = TimeText(S.WakeMinutes);
        SpeedBox.SelectedItem = SpeedBox.Items.Cast<ComboBoxItem>().First(i => (string)i.Tag == S.Speed || (string)i.Tag == "slow" && S.Speed is not ("medium" or "veryfast"));
        StartupBox.IsChecked = S.StartWithWindows;
        FullscreenBox.IsChecked = S.DisableFullscreen;
        LatBox.Text = S.Latitude?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
        LonBox.Text = S.Longitude?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
        UpdateValueLabels();
        UpdateRangeInfo();
        _loading = false;
        Redraw();
    }

    void HookSlider(Slider slider, Action<int> store)
    {
        slider.ValueChanged += (_, e) =>
        {
            if (_loading) return;
            int v = (int)(Math.Round(e.NewValue / 50) * 50);
            store(v);
            S.Preset = Presets.All.FirstOrDefault(p => p.Day == S.Day && p.Night == S.Night && p.Late == S.Late)?.Name ?? "Custom";
            S.Save();
            // Show the colour being chosen on screen for a moment, then go back to the schedule.
            _engine.PreviewKelvin = v;
            _previewEnd.Stop(); _previewEnd.Start();
            _engine.Refresh();
            _loading = true;
            PresetBox.SelectedItem = S.Preset;
            PresetInfo.Text = Presets.All.FirstOrDefault(p => p.Name == S.Preset)?.Description ?? "Your own colours";
            _loading = false;
            UpdateValueLabels();
            Redraw();
        };
    }

    void UpdateValueLabels()
    {
        DayValue.Text = $"{S.Day}K"; NightValue.Text = $"{S.Night}K"; LateValue.Text = $"{S.Late}K";
    }

    void ShiftWake(int minutes)
    {
        S.WakeMinutes = ((S.WakeMinutes + minutes) % 1440 + 1440) % 1440;
        S.Save();
        WakeText.Text = TimeText(S.WakeMinutes);
        _engine.Refresh();
        Redraw();
    }

    void OnEngineChanged()
    {
        if (!IsVisible && !SmokeTest.Running) return;
        var now = DateTime.Now;
        BigKelvin.Text = $"{Math.Round(_engine.CurrentKelvin / 10) * 10:0}K";
        Clock.Text = now.ToString("t");

        var (what, when) = Schedule.NextEvent(now, S);
        Status.Text = _engine.PausedFor switch
        {
            "timer" or "sunrise" => $"Off until {_engine.DisabledUntil:t}",
            "full screen" => "Off while a full-screen app is open",
            string app => $"Off while {app} is in front",
            null when _engine.PreviewKelvin != null => "Previewing",
            null when _engine.MovieUntil != null => $"Movie mode until {_engine.MovieUntil:t}",
            null when _engine.ManualKelvin != null => $"Set by hand until the next change · {what} in {Span(when - now)}",
            _ => $"{_engine.Phase switch { Phase.Day => "Daytime", Phase.Night => "Evening", _ => "Bedtime" }} · {what} in {Span(when - now)}",
        };
        if (_engine.Effect != Core.Effect.None) Status.Text += $" · {ScreenColor.EffectName(_engine.Effect)}";
        if (S.Brightness < 1) Status.Text += $" · {S.Brightness:P0} brightness";

        string? banner = App.FluxRunning ? "f.lux is running too. Quit it (right-click its tray icon › Exit) so the two don't fight over your screen."
            : S.Latitude == null ? "Set your location under Options so sunrise and sunset are right. Until then GraveShift assumes 7:00 and 19:00."
            : _engine.Limited ? "Windows refused that colour. Very warm colours need the wider colour range under Options."
            : null;
        Banner.Visibility = banner == null ? Visibility.Collapsed : Visibility.Visible;
        BannerText.Text = banner ?? "";

        if (now - _graphDrawn > TimeSpan.FromMinutes(1)) Redraw();
    }

    // ---- graph ----

    void Redraw()
    {
        _graphDrawn = DateTime.Now;
        var g = Graph;
        g.Children.Clear();
        double w = g.ActualWidth > 0 ? g.ActualWidth : 532, h = g.Height;
        var now = DateTime.Now;
        const int steps = 144; // one bar per 10 minutes across 24 hours
        double maxM = 1e6 / 1200, minM = 1e6 / 6500;

        for (int i = 0; i < steps; i++)
        {
            var t = now.AddHours(-12).AddMinutes(i * 10);
            var (k, _) = Schedule.At(t, S);
            double level = (maxM - 1e6 / k) / (maxM - minM); // 1 = daylight, 0 = deepest ember
            double barH = 18 + level * (h - 44);
            var (r, gg, b) = ScreenColor.FromKelvin(k);
            var bar = new Rectangle
            {
                Width = w / steps + 1, Height = barH,
                Fill = new SolidColorBrush(Color.FromRgb((byte)(r * 255), (byte)(gg * 220), (byte)(b * 200))),
            };
            Canvas.SetLeft(bar, i * w / steps);
            Canvas.SetTop(bar, h - 22 - barH);
            g.Children.Add(bar);
        }

        // Hour labels every 6 hours, and the "now" marker in the middle.
        for (int i = 0; i <= 24; i++)
        {
            var t = now.AddHours(-12 + i);
            if (t.Hour % 6 != 0) continue;
            double x = (t - now.AddHours(-12)).TotalMinutes / 1440 * w - (t.Minute) / 1440.0 * w;
            var label = new TextBlock { Text = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0).ToString("t"), FontSize = 11, Foreground = (Brush)FindResource("Dim") };
            Canvas.SetLeft(label, Math.Clamp(x - 22, 2, w - 56)); Canvas.SetTop(label, h - 18);
            g.Children.Add(label);
        }
        var line = new Rectangle { Width = 2, Height = h - 22, Fill = Brushes.White, Opacity = 0.9 };
        Canvas.SetLeft(line, w / 2 - 1);
        g.Children.Add(line);
        var nowLabel = new TextBlock { Text = "now", FontSize = 11, Foreground = Brushes.White };
        Canvas.SetLeft(nowLabel, w / 2 + 5); Canvas.SetTop(nowLabel, 4);
        g.Children.Add(nowLabel);

        var (rise, set, up) = Schedule.SunToday(DateOnly.FromDateTime(now), S);
        SunLine.Text = rise is double rr && set is double ss
            ? $"☀ {TimeText((int)rr)}   ☾ {TimeText((int)ss)}   bed {TimeText(S.WakeMinutes - Schedule.BedtimeBeforeWake)}"
            : up ? "Sun up all day" : "Sun down all day";
    }

    // ---- options ----

    void SaveTypedLocation()
    {
        if (double.TryParse(LatBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(LonBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) &&
            lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
        {
            SetLocation(lat, lon, "Saved.");
        }
        else LocationInfo.Text = "That isn't a valid location. Latitude goes from -90 to 90, longitude from -180 to 180 (for example 51.5 and -0.12).";
    }

    async Task AskWindowsForLocation()
    {
        LocationInfo.Text = "Asking Windows…";
        try
        {
            var access = await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
            if (access != Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
            {
                LocationInfo.Text = "Windows location is turned off. Type your latitude and longitude instead, or turn it on in Settings › Privacy › Location.";
                return;
            }
            var pos = await new Windows.Devices.Geolocation.Geolocator().GetGeopositionAsync();
            SetLocation(Math.Round(pos.Coordinate.Point.Position.Latitude, 3), Math.Round(pos.Coordinate.Point.Position.Longitude, 3), "Found by Windows.");
        }
        catch (Exception ex) { LocationInfo.Text = "Windows couldn't find your location: " + ex.Message; }
    }

    void SetLocation(double lat, double lon, string note)
    {
        S.Latitude = lat; S.Longitude = lon; S.Save();
        LatBox.Text = lat.ToString("0.###", CultureInfo.InvariantCulture);
        LonBox.Text = lon.ToString("0.###", CultureInfo.InvariantCulture);
        LocationInfo.Text = note;
        _engine.Refresh();
        Redraw();
    }

    const string IcmKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM";

    static bool RangeUnlocked()
    {
        using var k = Registry.LocalMachine.OpenSubKey(IcmKey);
        return k?.GetValue("GdiIcmGammaRange") is int v && v == 256;
    }

    void UpdateRangeInfo()
    {
        bool ok = RangeUnlocked();
        RangeInfo.Text = ok ? "Full colour range is on: colours down to 1200K work."
                            : "Windows limits how warm the screen can go. Unlock the full range for candle and ember colours (needs a restart).";
        RangeButton.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
    }

    void UnlockRange()
    {
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("reg.exe",
                $"add \"HKLM\\{IcmKey}\" /v GdiIcmGammaRange /t REG_DWORD /d 256 /f") { UseShellExecute = true, Verb = "runas", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
            p?.WaitForExit();
        }
        catch (Win32Exception) { /* the user said no to the admin prompt */ }
        UpdateRangeInfo();
        if (RangeUnlocked()) RangeInfo.Text = "Unlocked. Restart Windows to finish.";
    }

    // ---- text ----

    static string TimeText(int minutes)
    {
        minutes = ((minutes % 1440) + 1440) % 1440;
        return DateTime.Today.AddMinutes(minutes).ToString("t");
    }

    static string Span(TimeSpan t) =>
        t.TotalMinutes < 1 ? "under a minute"
        : t.TotalHours < 1 ? $"{(int)t.TotalMinutes} min"
        : $"{(int)t.TotalHours} h {t.Minutes} min";
}
