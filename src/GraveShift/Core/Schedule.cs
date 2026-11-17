namespace GraveShift.Core;

public enum Phase { Day, Night, Late }

/// <summary>What colour the screen should be at a given moment, from the settings alone.</summary>
public static class Schedule
{
    /// <summary>Bedtime colour starts this long before wake time.</summary>
    public const int BedtimeBeforeWake = 510;

    // Used until the user sets a location.
    const double DefaultRise = 7 * 60, DefaultSet = 19 * 60;

    public static (double? Rise, double? Set, bool AlwaysUp) SunToday(DateOnly date, Settings s) =>
        s.Latitude is double lat && s.Longitude is double lon ? Sun.Times(date, lat, lon) : (DefaultRise, DefaultSet, false);

    public static (double Kelvin, Phase Phase) At(DateTime local, Settings s)
    {
        double t = local.TimeOfDay.TotalMinutes, l = s.RampMinutes;
        var (rise, set, alwaysUp) = SunToday(DateOnly.FromDateTime(local), s);

        double dayness = rise is double r && set is double st
            ? Clamp01((t - r) / l + 0.5) * Clamp01((st - t) / l + 0.5)
            : alwaysUp ? 1 : 0;

        double u = Mod(t - (s.WakeMinutes - BedtimeBeforeWake), 1440); // minutes since bedtime
        double lateness = u < BedtimeBeforeWake ? Math.Min(Clamp01(u / l), Clamp01((BedtimeBeforeWake - u) / l)) : 0;

        double night = ScreenColor.Mix(s.Night, s.Late, lateness);
        double k = ScreenColor.Mix(night, s.Day, dayness);
        var phase = dayness >= 0.5 ? Phase.Day : lateness >= 0.5 ? Phase.Late : Phase.Night;
        return (k, phase);
    }

    /// <summary>The next sunrise, sunset or bedtime after <paramref name="now"/>, for the status line.</summary>
    public static (string What, DateTime When) NextEvent(DateTime now, Settings s)
    {
        var events = new List<(string, DateTime)>();
        for (int d = 0; d <= 2; d++)
        {
            var day = now.Date.AddDays(d);
            var (rise, set, _) = SunToday(DateOnly.FromDateTime(day), s);
            if (rise is double r) events.Add(("Sunrise", day.AddMinutes(r)));
            if (set is double st) events.Add(("Sunset", day.AddMinutes(st)));
            events.Add(("Bedtime", day.AddMinutes(s.WakeMinutes - BedtimeBeforeWake)));
        }
        return events.Where(e => e.Item2 > now).OrderBy(e => e.Item2).FirstOrDefault(("Sunrise", now.AddDays(1)));
    }

    public static DateTime NextSunrise(DateTime now, Settings s)
    {
        for (int d = 0; d <= 2; d++)
        {
            var day = now.Date.AddDays(d);
            if (SunToday(DateOnly.FromDateTime(day), s).Rise is double r && day.AddMinutes(r) > now) return day.AddMinutes(r);
        }
        return now.AddHours(12); // polar night: give it half a day
    }

    static double Clamp01(double v) => Math.Clamp(v, 0, 1);
    static double Mod(double a, double m) => ((a % m) + m) % m;
}
