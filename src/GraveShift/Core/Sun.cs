namespace GraveShift.Core;

/// <summary>Sunrise and sunset from the NOAA solar equations (good to about a minute).</summary>
public static class Sun
{
    /// <summary>Minutes after local midnight. Null rise/set means the sun never crosses the horizon that day;
    /// then <paramref name="alwaysUp"/> says whether it's polar day or polar night.</summary>
    public static (double? Rise, double? Set, bool AlwaysUp) Times(DateOnly date, double lat, double lon, double utcOffsetHours)
    {
        double g = 2 * Math.PI / 365 * (date.DayOfYear - 1);
        double eqTime = 229.18 * (0.000075 + 0.001868 * Math.Cos(g) - 0.032077 * Math.Sin(g)
                                  - 0.014615 * Math.Cos(2 * g) - 0.040849 * Math.Sin(2 * g));
        double decl = 0.006918 - 0.399912 * Math.Cos(g) + 0.070257 * Math.Sin(g) - 0.006758 * Math.Cos(2 * g)
                      + 0.000907 * Math.Sin(2 * g) - 0.002697 * Math.Cos(3 * g) + 0.00148 * Math.Sin(3 * g);
        double latR = lat * Math.PI / 180;
        double cosHa = Math.Cos(90.833 * Math.PI / 180) / (Math.Cos(latR) * Math.Cos(decl)) - Math.Tan(latR) * Math.Tan(decl);
        if (cosHa > 1) return (null, null, false);
        if (cosHa < -1) return (null, null, true);
        double ha = Math.Acos(cosHa) * 180 / Math.PI;
        double rise = 720 - 4 * (lon + ha) - eqTime + utcOffsetHours * 60;
        double set = 720 - 4 * (lon - ha) - eqTime + utcOffsetHours * 60;
        return (rise, set, false);
    }

    public static (double? Rise, double? Set, bool AlwaysUp) Times(DateOnly date, double lat, double lon) =>
        Times(date, lat, lon, TimeZoneInfo.Local.GetUtcOffset(date.ToDateTime(new TimeOnly(12, 0))).TotalHours);
}
