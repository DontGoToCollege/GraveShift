namespace GraveShift.Core;

public enum Effect { None, Darkroom, BlueSky, EmeraldCity, SaltLamp, SoftWhite }

/// <summary>Colour temperature → screen multipliers, and the gamma ramp Windows is given.</summary>
public static class ScreenColor
{
    /// <summary>Blackbody colour as red/green/blue multipliers (0..1), scaled so 6500K is plain white.</summary>
    public static (double R, double G, double B) FromKelvin(double kelvin)
    {
        var (r, g, b) = Raw(kelvin);
        var (wr, wg, wb) = Raw(6500);
        return (Math.Min(1, r / wr), Math.Min(1, g / wg), Math.Min(1, b / wb));
    }

    // Curve fit of the blackbody locus (Tanner Helland's published approximation), in 0..255.
    static (double, double, double) Raw(double kelvin)
    {
        double t = Math.Clamp(kelvin, 1000, 40000) / 100;
        double r = t <= 66 ? 255 : 329.698727446 * Math.Pow(t - 60, -0.1332047592);
        double g = t <= 66 ? 99.4708025861 * Math.Log(t) - 161.1195681661 : 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
        double b = t >= 66 ? 255 : t <= 19 ? 0 : 138.5177312231 * Math.Log(t - 10) - 305.0447927307;
        return (Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
    }

    /// <summary>Glide between two temperatures in mireds, which looks even to the eye.</summary>
    public static double Mix(double fromK, double toK, double f) =>
        1e6 / (1e6 / fromK + (1e6 / toK - 1e6 / fromK) * Math.Clamp(f, 0, 1));

    /// <summary>The 3×256 gamma ramp (red, green, blue) for one setting.</summary>
    public static ushort[] Ramp(double kelvin, double brightness, Effect effect)
    {
        var (r, g, b) = FromKelvin(kelvin);
        switch (effect)
        {
            case Effect.BlueSky: g *= 0.92; r *= 0.78; break;
            case Effect.EmeraldCity: r *= 0.55; b *= 0.7; break;
            case Effect.SaltLamp: g *= 0.62; b *= 0.5; break;
        }
        var ramp = new ushort[768];
        for (int i = 0; i < 256; i++)
        {
            double x = i / 255.0;
            if (effect == Effect.SoftWhite) x = 0.07 + 0.86 * x; // lifted blacks, gentler whites: like paper
            double rr = x * r, gg = x * g, bb = x * b;
            if (effect == Effect.Darkroom) { rr = 1 - x; gg = bb = 0; }
            ramp[i] = Word(rr * brightness);
            ramp[256 + i] = Word(gg * brightness);
            ramp[512 + i] = Word(bb * brightness);
        }
        return ramp;
    }

    static ushort Word(double v) => (ushort)Math.Round(Math.Clamp(v, 0, 1) * 65535);

    /// <summary>Colours for "Colour right now" in the menu.</summary>
    public static readonly (string Name, int Kelvin)[] Named =
    {
        ("Ember", 1200), ("Candle", 1900), ("Dim incandescent", 2300), ("Incandescent", 2700),
        ("Halogen", 3400), ("Fluorescent", 4200), ("Sunlight", 5500), ("Daylight", 6500),
    };

    public static string EffectName(Effect e) => e switch
    {
        Effect.Darkroom => "Darkroom",
        Effect.BlueSky => "Blue sky",
        Effect.EmeraldCity => "Emerald",
        Effect.SaltLamp => "Salt lamp",
        Effect.SoftWhite => "Soft white",
        _ => "None",
    };
}
