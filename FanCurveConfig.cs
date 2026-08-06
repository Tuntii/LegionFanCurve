using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegionFanCurve;

internal sealed record CurvePoint(
    [property: JsonPropertyName("cpu")] int Cpu,
    [property: JsonPropertyName("gpu")] int Gpu,
    [property: JsonPropertyName("rpm")] int Rpm);

internal sealed class FanCurveConfig
{
    [JsonPropertyName("legionGen")] public int LegionGen { get; set; } = 5;
    [JsonPropertyName("maxRpm")] public int MaxRpm { get; set; } = 4400;
    [JsonPropertyName("accel")] public int Accel { get; set; } = 2;
    [JsonPropertyName("decel")] public int Decel { get; set; } = 2;
    [JsonPropertyName("hysteresis")] public int Hysteresis { get; set; } = 3;
    [JsonPropertyName("points")] public List<CurvePoint> Points { get; set; } = new();

    public static FanCurveConfig Default4400() => new()
    {
        LegionGen = 5,
        MaxRpm = 4400,
        Accel = 2,
        Decel = 2,
        Hysteresis = 3,
        Points =
        [
            new(40, 42, 1800),
            new(50, 52, 2200),
            new(58, 60, 2600),
            new(65, 66, 3000),
            new(72, 72, 3400),
            new(78, 78, 3800),
            new(84, 84, 4100),
            new(90, 90, 4300),
            new(95, 95, 4400)
        ]
    };

    public static string ConfigPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LegionFanCurve");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "curve.json");
        }
    }

    public static FanCurveConfig Load()
    {
        try
        {
            foreach (var path in new[] { ConfigPath, Path.Combine(AppContext.BaseDirectory, "curve.json") })
            {
                if (!File.Exists(path)) continue;
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<FanCurveConfig>(json, JsonOpts());
                if (cfg?.Points is { Count: > 0 })
                {
                    cfg.MaxRpm = Math.Clamp(cfg.MaxRpm, 1000, 5500);
                    cfg.Points = cfg.Points
                        .Select(p => p with { Rpm = Math.Min(p.Rpm, cfg.MaxRpm) })
                        .ToList();
                    return cfg;
                }
            }
        }
        catch { /* fall through */ }
        return Default4400();
    }

    public void Save()
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptsPretty()));
    }

    private static JsonSerializerOptions JsonOpts() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static JsonSerializerOptions JsonOptsPretty() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
