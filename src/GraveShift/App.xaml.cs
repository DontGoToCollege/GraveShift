using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using GraveShift.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace GraveShift;

public partial class App : Application
{
    public static string Version => typeof(App).Assembly.GetName().Version!.ToString(3);

    Mutex? _single;
    EventWaitHandle? _showSignal;
    Engine _engine = null!;
    Forms.NotifyIcon? _tray;
    MainWindow? _window;
    HwndSource? _hotkeys;
    int _trayKelvin = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--smoke")) { Shutdown(SmokeTest.Run()); return; }

        _single = new Mutex(true, "GraveShift.Single", out bool first);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "GraveShift.Show");
        if (!first) { _showSignal.Set(); Shutdown(); return; } // already running: just bring its window up

        // Never leave the screen tinted if something goes wrong.
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Native.RestoreRamp();
        DispatcherUnhandledException += (_, ex) => { Log(ex.Exception.ToString()); ex.Handled = true; };
        SessionEnding += (_, _) => _engine.Stop();

        var settings = Settings.Load();
        if (settings.StartWithWindows) SetStartup(true); // re-point the Run entry if the exe moved
        _engine = new Engine(settings, applyToScreen: true);
        _engine.Changed += UpdateTray;

        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.Invoke(_engine.Refresh);
        SystemEvents.PowerModeChanged += (_, p) => { if (p.Mode == PowerModes.Resume) Dispatcher.Invoke(_engine.Refresh); };
        SystemEvents.SessionSwitch += (_, _) => Dispatcher.Invoke(_engine.Refresh);

        CreateTray();
        RegisterHotkeys();
        _engine.Start();

        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.Invoke(ShowWindow), null, -1, false);
        if (!e.Args.Contains("--tray")) ShowWindow();
    }

    void ShowWindow()
    {
        _window ??= new MainWindow(_engine);
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    void ExitApp()
    {
        _engine.Stop();
        _hotkeys?.Dispose();
        if (_tray != null) _tray.Visible = false;
        Shutdown();
    }

    // ---- tray ----

    void CreateTray()
    {
        using var s = GetResourceStream(new Uri("pack://application:,,,/Assets/GraveShift.ico"))!.Stream;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(s), Text = "GraveShift", Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowWindow(); };
        _tray.ContextMenuStrip = new Forms.ContextMenuStrip();
        _tray.ContextMenuStrip.Opening += (_, _) => BuildMenu(_tray.ContextMenuStrip);
        BuildMenu(_tray.ContextMenuStrip);
    }

    void UpdateTray()
    {
        int k = (int)Math.Round(_engine.CurrentKelvin / 100) * 100;
        if (_tray == null || k == _trayKelvin) return;
        _trayKelvin = k;
        _tray.Text = _engine.PausedFor != null ? "GraveShift (paused)" : $"GraveShift {k}K";
    }

    void BuildMenu(Forms.ContextMenuStrip m)
    {
        m.Items.Clear();
        var s = _engine.S;
        Forms.ToolStripMenuItem Item(string text, Action click, bool check = false)
        {
            var i = new Forms.ToolStripMenuItem(text) { Checked = check };
            i.Click += (_, _) => click();
            return i;
        }

        var open = Item("Open GraveShift", ShowWindow);
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        m.Items.Add(open);
        m.Items.Add(new Forms.ToolStripSeparator());

        var off = new Forms.ToolStripMenuItem("Turn off");
        if (_engine.DisabledUntil != null) off.DropDownItems.Add(Item("Turn back on", _engine.Enable));
        off.DropDownItems.Add(Item("For an hour (Alt+End)", () => _engine.DisableFor(TimeSpan.FromHours(1)), _engine.DisabledWhy == "timer"));
        off.DropDownItems.Add(Item("Until sunrise", _engine.DisableUntilSunrise, _engine.DisabledWhy == "sunrise"));
        off.DropDownItems.Add(Item("For full-screen apps", () => { s.DisableFullscreen = !s.DisableFullscreen; s.Save(); _engine.Refresh(); }, s.DisableFullscreen));
        foreach (var app in s.DisabledApps.Union(_engine.LastApp is string a ? new[] { a } : [], StringComparer.OrdinalIgnoreCase).ToList())
            off.DropDownItems.Add(Item($"For {app}", () => ToggleApp(app), s.DisabledApps.Contains(app, StringComparer.OrdinalIgnoreCase)));
        m.Items.Add(off);

        var fx = new Forms.ToolStripMenuItem("Effects");
        fx.DropDownItems.Add(Item("Movie mode (2½ hours)", _engine.ToggleMovie, _engine.MovieUntil != null));
        fx.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var e in Enum.GetValues<Effect>())
            fx.DropDownItems.Add(Item(ScreenColor.EffectName(e), () => _engine.SetEffect(e), _engine.Effect == e));
        m.Items.Add(fx);

        var now = new Forms.ToolStripMenuItem("Colour right now");
        now.DropDownItems.Add(Item("Follow the schedule", () => _engine.SetManual(null), _engine.ManualKelvin == null));
        foreach (var (name, k) in ScreenColor.Named)
            now.DropDownItems.Add(Item($"{name}  {k}K", () => _engine.SetManual(k), _engine.ManualKelvin == k));
        m.Items.Add(now);

        var presets = new Forms.ToolStripMenuItem("Presets");
        foreach (var p in Presets.All)
            presets.DropDownItems.Add(Item(p.Name, () => ApplyPreset(p), s.Preset == p.Name));
        m.Items.Add(presets);

        m.Items.Add(new Forms.ToolStripSeparator());
        m.Items.Add(Item("Exit", ExitApp));
    }

    void ToggleApp(string app)
    {
        var list = _engine.S.DisabledApps;
        if (list.RemoveAll(x => x.Equals(app, StringComparison.OrdinalIgnoreCase)) == 0) list.Add(app);
        _engine.S.Save();
        _engine.Refresh();
    }

    public void ApplyPreset(Preset p)
    {
        var s = _engine.S;
        (s.Day, s.Night, s.Late, s.Preset) = (p.Day, p.Night, p.Late, p.Name);
        s.Save();
        _engine.Refresh();
        _window?.LoadFromSettings();
    }

    // ---- hotkeys ----

    void RegisterHotkeys()
    {
        _hotkeys = new HwndSource(new HwndSourceParameters("GraveShift hotkeys") { ParentWindow = new IntPtr(-3) }); // message-only window
        const uint Alt = 1, Shift = 4, NoRepeat = 0x4000;
        (int id, uint mods, uint vk)[] keys =
        {
            (1, Alt | NoRepeat, 0x23),          // Alt+End: off for an hour / back on
            (2, Alt, 0x21), (3, Alt, 0x22),     // Alt+PgUp/PgDn: brightness
            (4, Alt | Shift, 0x21), (5, Alt | Shift, 0x22), // Alt+Shift+PgUp/PgDn: colour
        };
        foreach (var (id, mods, vk) in keys) Native.RegisterHotKey(_hotkeys.Handle, id, mods, vk); // taken by another app: that key just won't work
        _hotkeys.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (msg != 0x0312) return IntPtr.Zero;
            switch ((int)w)
            {
                case 1: _engine.ToggleHour(); break;
                case 2: _engine.NudgeBrightness(0.1); break;
                case 3: _engine.NudgeBrightness(-0.1); break;
                case 4: _engine.NudgeColor(200); break;
                case 5: _engine.NudgeColor(-200); break;
            }
            handled = true;
            return IntPtr.Zero;
        });
    }

    // ---- helpers ----

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetStartup(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("GraveShift", $"\"{Environment.ProcessPath}\" --tray");
        else k.DeleteValue("GraveShift", false);
    }

    public static void Log(string text)
    {
        try { File.AppendAllText(Path.Combine(Settings.DataDir, "log.txt"), $"{DateTime.Now:s} {text}\n"); } catch { }
    }

    public static bool FluxRunning => Process.GetProcessesByName("flux").Length > 0;
}
