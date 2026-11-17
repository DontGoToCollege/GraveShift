using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GraveShift.Core;
using Microsoft.Win32;
using Path = System.Windows.Shapes.Path;

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

        OptionsToggle.Click += (_, _) => ShowOptions(OptionsPanel.Visibility != Visibility.Visible);
        SizeChanged += (_, _) => KeepOnScreen();
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
        double w = g.ActualWidth > 0 ? g.ActualWidth : 532, h = g.Height, baseY = h - 24;
        GraphFrame.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 12, 12); // keep the curve inside the rounded corners
        var now = DateTime.Now;
        var start = now.AddHours(-12);

        // Sample every 5 minutes, then soften the corners where a ramp starts or ends so the curve flows.
        const int n = 289;
        var mired = new double[n];
        for (int i = 0; i < n; i++) mired[i] = 1e6 / Schedule.At(start.AddMinutes(i * 5), S).Kelvin;
        mired = Smooth(mired, 3.5);
        double maxM = 1e6 / 1200, minM = 1e6 / 6500;
        double X(int i) => i * w / (n - 1);
        double Y(int i) => baseY - 12 - (maxM - mired[i]) / (maxM - minM) * (baseY - 34);

        // Faint guide lines every 6 hours, under the curve.
        var dim = (Brush)FindResource("Dim");
        for (int i = 0; i <= 24; i++)
        {
            var hour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0).AddHours(i);
            if (hour.Hour % 6 != 0 || hour <= start) continue;
            double x = (hour - start).TotalMinutes / 1440 * w;
            if (x > w) break;
            var guide = new Line { X1 = x, X2 = x, Y1 = 0, Y2 = baseY, Stroke = dim, StrokeThickness = 1, Opacity = 0.15 };
            g.Children.Add(guide);
            var label = new TextBlock { Text = hour.ToString("t"), FontSize = 11, Foreground = dim };
            Canvas.SetLeft(label, Math.Clamp(x - 22, 6, w - 58)); Canvas.SetTop(label, baseY + 5);
            g.Children.Add(label);
        }

        // Colour runs left to right with the schedule, so the fill is one smooth gradient.
        var colours = new GradientStopCollection();
        for (int i = 0; i < n; i += 4)
        {
            var (r, gg, b) = ScreenColor.FromKelvin(1e6 / mired[i]);
            colours.Add(new GradientStop(Color.FromRgb((byte)(r * 255), (byte)(gg * 222), (byte)(b * 205)), (double)i / (n - 1)));
        }
        var across = new LinearGradientBrush(colours, new Point(0, 0), new Point(1, 0));

        var area = new StreamGeometry();
        var edge = new StreamGeometry();
        using (var c = area.Open())
        {
            c.BeginFigure(new Point(0, baseY), true, true);
            for (int i = 0; i < n; i++) c.LineTo(new Point(X(i), Y(i)), true, true);
            c.LineTo(new Point(w, baseY), true, true);
        }
        using (var c = edge.Open())
        {
            c.BeginFigure(new Point(X(0), Y(0)), false, false);
            for (int i = 1; i < n; i++) c.LineTo(new Point(X(i), Y(i)), true, true);
        }
        area.Freeze(); edge.Freeze();

        g.Children.Add(new Path
        {
            Data = area, Fill = across,
            // Bright at the top edge, fading into the panel toward the bottom.
            OpacityMask = new LinearGradientBrush(Color.FromArgb(235, 0, 0, 0), Color.FromArgb(40, 0, 0, 0), 90),
        });
        g.Children.Add(new Path
        {
            Data = edge, Stroke = across, StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 0, Color = Colors.White, Opacity = 0.35 },
        });

        // "Now": a soft line and a dot riding on the curve.
        int mid = (n - 1) / 2;
        g.Children.Add(new Line { X1 = w / 2, X2 = w / 2, Y1 = 8, Y2 = baseY, Stroke = Brushes.White, StrokeThickness = 1.2, Opacity = 0.45 });
        var dot = new Ellipse { Width = 12, Height = 12, Fill = Brushes.White, Stroke = (Brush)FindResource("Bg"), StrokeThickness = 2.5 };
        Canvas.SetLeft(dot, w / 2 - 6); Canvas.SetTop(dot, Y(mid) - 6);
        g.Children.Add(dot);
        var nowLabel = new TextBlock { Text = "now", FontSize = 11, Foreground = Brushes.White, Opacity = 0.85 };
        Canvas.SetLeft(nowLabel, w / 2 + 6); Canvas.SetTop(nowLabel, 6);
        g.Children.Add(nowLabel);

        var (rise, set, up) = Schedule.SunToday(DateOnly.FromDateTime(now), S);
        SunLine.Text = rise is double rr && set is double ss
            ? $"☀ {TimeText((int)rr)}   ☾ {TimeText((int)ss)}   bed {TimeText(S.WakeMinutes - Schedule.BedtimeBeforeWake)}"
            : up ? "Sun up all day" : "Sun down all day";
    }

    /// <summary>Gaussian blur of a series (ends held flat), used only for drawing.</summary>
    static double[] Smooth(double[] v, double sigma)
    {
        int r = (int)Math.Ceiling(sigma * 3);
        var k = Enumerable.Range(-r, 2 * r + 1).Select(i => Math.Exp(-i * i / (2 * sigma * sigma))).ToArray();
        double sum = k.Sum();
        var o = new double[v.Length];
        for (int i = 0; i < v.Length; i++)
            for (int j = -r; j <= r; j++) o[i] += v[Math.Clamp(i + j, 0, v.Length - 1)] * k[j + r] / sum;
        return o;
    }

    // ---- options ----

    public void ShowOptions(bool open)
    {
        OptionsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        OptionsToggle.Tag = open ? "open" : null;
        System.Windows.Automation.AutomationProperties.SetName(OptionsToggle, open ? "Hide options" : "Show options");
    }

    /// <summary>After the window grows (options opened), slide it back inside the screen it's on.</summary>
    void KeepOnScreen()
    {
        if (!IsVisible) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        double right = area.Right / dpi.DpiScaleX, bottom = area.Bottom / dpi.DpiScaleY;
        double left = area.Left / dpi.DpiScaleX, top = area.Top / dpi.DpiScaleY;
        if (Left + ActualWidth > right) Left = Math.Max(left, right - ActualWidth);
        if (Top + ActualHeight > bottom) Top = Math.Max(top, bottom - ActualHeight);
    }

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
