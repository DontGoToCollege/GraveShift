using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GraveShift.Core;

namespace GraveShift;

/// <summary>`GraveShift.exe --smoke`: checks the maths and renders the window to smoke.png, without touching the screen's colours.
/// Writes smoke.txt to GraveShift-Data and exits 0 if every check passed.</summary>
static class SmokeTest
{
    public static bool Running { get; private set; }

    public static int Run()
    {
        Running = true;
        var log = new List<string>();
        int fails = 0;
        void Check(string name, bool ok, string detail = "")
        {
            log.Add($"{(ok ? "ok  " : "FAIL")} {name} {detail}");
            if (!ok) fails++;
        }

        // London on the June solstice: sunrise 04:43, sunset 21:21 (BST, UTC+1).
        var (rise, set, _) = Sun.Times(new DateOnly(2026, 6, 21), 51.5074, -0.1278, 1);
        Check("London sunrise", Math.Abs(rise!.Value - (4 * 60 + 43)) < 4, $"{rise:0.0}");
        Check("London sunset", Math.Abs(set!.Value - (21 * 60 + 21)) < 4, $"{set:0.0}");
        var polar = Sun.Times(new DateOnly(2026, 12, 21), 78.2, 15.6, 1);
        Check("Svalbard winter has no sunrise", polar.Rise == null && !polar.AlwaysUp);

        var white = ScreenColor.FromKelvin(6500);
        Check("6500K is white", white.R > 0.99 && white.G > 0.99 && white.B > 0.99, $"{white}");
        var candle = ScreenColor.FromKelvin(1900);
        Check("1900K has no blue", candle.B < 0.01 && candle.R > 0.99, $"{candle}");
        Check("mired mix midpoint", Math.Abs(ScreenColor.Mix(6500, 3250, 0.5) - 4333) < 2);

        var ramp = ScreenColor.Ramp(3400, 0.5, Effect.None);
        Check("ramp halves brightness", ramp[255] == 32768 && ramp[0] == 0, $"{ramp[255]}");
        var dark = ScreenColor.Ramp(6500, 1, Effect.Darkroom);
        Check("darkroom is inverted red", dark[0] == 65535 && dark[255] == 0 && dark[256 + 128] == 0);

        // The schedule uses this PC's time zone, so put the test location on this zone's own meridian.
        var s = new Settings { Latitude = 51.5, Longitude = TimeZoneInfo.Local.BaseUtcOffset.TotalHours * 15, WakeMinutes = 7 * 60 };
        var day = new DateTime(2026, 6, 21);
        set = Schedule.SunToday(DateOnly.FromDateTime(day), s).Set;
        var noon = Schedule.At(day.AddHours(12), s);
        Check("noon is daytime", noon.Phase == Phase.Day && Math.Abs(noon.Kelvin - 6500) < 1, $"{noon.Kelvin:0}");
        var eve = Schedule.At(day.AddHours(22), s); // after sunset, before 22:30 bedtime
        Check("22:00 is evening", eve.Phase == Phase.Night && Math.Abs(eve.Kelvin - 3400) < 50, $"{eve.Kelvin:0}");
        var late = Schedule.At(day.AddHours(24 + 1.5), s);
        Check("01:30 is bedtime", late.Phase == Phase.Late && Math.Abs(late.Kelvin - 1900) < 50, $"{late.Kelvin:0}");
        var sunsetMid = Schedule.At(day.AddMinutes(set!.Value), s);
        Check("sunset is halfway", sunsetMid.Kelvin is > 4000 and < 5000, $"{sunsetMid.Kelvin:0}");
        Check("next event after noon is sunset", Schedule.NextEvent(day.AddHours(12), s).What == "Sunset");

        // Render the real window offscreen, with an engine that never touches the screen.
        try
        {
            var engine = new Engine(Settings.Load(), applyToScreen: false);
            engine.Start();
            var w = new MainWindow(engine);
            var root = (FrameworkElement)w.Content;
            w.Content = null;
            root.Measure(new Size(564, double.PositiveInfinity));
            root.Arrange(new Rect(root.DesiredSize));
            root.UpdateLayout();
            w.LoadFromSettings();
            root.UpdateLayout();
            var dpi = 1.75;
            var bmp = new RenderTargetBitmap((int)(root.ActualWidth * dpi), (int)(root.ActualHeight * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
            bmp.Render(root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var f = File.Create(Path.Combine(Settings.DataDir, "smoke.png"));
            enc.Save(f);
            engine.Stop();
            Check("window renders", root.ActualHeight > 300, $"{root.ActualWidth:0}x{root.ActualHeight:0}");
        }
        catch (Exception ex) { Check("window renders", false, ex.ToString()); }

        log.Add(fails == 0 ? "ALL PASSED" : $"{fails} FAILED");
        File.WriteAllLines(Path.Combine(Settings.DataDir, "smoke.txt"), log);
        return fails == 0 ? 0 : 1;
    }
}
