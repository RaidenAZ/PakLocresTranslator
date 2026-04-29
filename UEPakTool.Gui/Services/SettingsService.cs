using System.IO;
using System.Text.Json;
using UEPakTool.Gui.Models;

namespace UEPakTool.Gui.Services;

public class SettingsService
{
    private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void EnsureConfigDir() => Directory.CreateDirectory(ConfigDir);

    public static AiConfig LoadAiConfig()
    {
        var path = Path.Combine(ConfigDir, "ai-config.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AiConfig>(json) ?? new AiConfig();
        }
        return new AiConfig();
    }

    public static void SaveAiConfig(AiConfig config)
    {
        EnsureConfigDir();
        File.WriteAllText(Path.Combine(ConfigDir, "ai-config.json"),
            JsonSerializer.Serialize(config, JsonOptions));
    }

    public static List<GlossaryEntry> LoadGlossary()
    {
        var path = Path.Combine(ConfigDir, "glossary.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<GlossaryEntry>>(json) ?? [];
        }
        return [];
    }

    public static void SaveGlossary(List<GlossaryEntry> glossary)
    {
        EnsureConfigDir();
        File.WriteAllText(Path.Combine(ConfigDir, "glossary.json"),
            JsonSerializer.Serialize(glossary, JsonOptions));
    }
}
