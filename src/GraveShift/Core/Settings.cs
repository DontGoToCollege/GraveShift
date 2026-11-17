using System.Text.Json;

namespace GraveShift.Core;

/// <summary>Everything the user can set. Saved as JSON in GraveShift-Data next to the exe.</summary>
public sealed class Settings
{
    public int Day { get; set; } = 6500;
    public int Night { get; set; } = 3400;
    public int Late { get; set; } = 1900;
    public string Preset { get; set; } = "Recommended";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int WakeMinutes { get; set; } = 7 * 60;
    public string Speed { get; set; } = "slow"; // veryfast | medium | slow
    public double Brightness { get; set; } = 1.0;
    public bool DisableFullscreen { get; set; }
    public List<string> DisabledApps { get; set; } = new();
    public bool StartWithWindows { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DataDir
    {
        get
        {
            var portable = Path.Combine(AppContext.BaseDirectory, "GraveShift-Data");
            try { Directory.CreateDirectory(portable); File.WriteAllText(Path.Combine(portable, ".write-test"), ""); return portable; }
            catch
            {
                var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GraveShift");
                Directory.CreateDirectory(local);
                return local;
            }
        }
    }

    static string FilePath => Path.Combine(DataDir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* a broken file falls back to defaults; the next save replaces it */ }
        return new();
    }

    public void Save()
    {
        // Write then swap, so a crash mid-save never leaves a half-written settings file.
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, FilePath, true);
    }

    /// <summary>Minutes the schedule takes to glide between colours.</summary>
    public double RampMinutes => Speed switch { "veryfast" => 1.0 / 3, "medium" => 20, _ => 60 };
}

public record Preset(string Name, string Description, int Day, int Night, int Late);

public static class Presets
{
    public static readonly Preset[] All =
    {
        new("Recommended", "Warm at sunset, candlelight before bed", 6500, 3400, 1900),
        new("Easy on the eyes", "Softer all day and night", 5900, 3600, 2500),
        new("Classic", "Warm from sunset right through the night", 6500, 3400, 3400),
        new("Working late", "Bright after sunset, winds down for bed", 6500, 6500, 2300),
        new("Far north", "A hint of sunset, candlelight at bedtime", 6500, 5500, 1900),
        new("Graveyard", "Extra-warm light all the time", 2700, 2300, 1500),
        new("Colour work", "Small changes, kinder to colour accuracy", 6500, 5000, 3400),
    };
}
