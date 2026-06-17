using System.IO;
using System.Text.Json;
using UEPakTool.Gui.Models;

namespace UEPakTool.Gui.Services;

public class SettingsService : ISettingsService
{
    private readonly string _configDir;

    public SettingsService() : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config"))
    {
    }

    public SettingsService(string configDir)
    {
        _configDir = configDir;
    }

    public AiConfig LoadAiConfig()
    {
        var path = Path.Combine(_configDir, "ai-config.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AiConfig>(json) ?? new AiConfig();
        }
        return new AiConfig();
    }

    public void SaveAiConfig(AiConfig config)
    {
        EnsureConfigDir();
        File.WriteAllText(Path.Combine(_configDir, "ai-config.json"),
            JsonExportService.BuildAiConfigJson(config));
    }

    public List<GlossaryEntry> LoadGlossary()
    {
        var path = Path.Combine(_configDir, "glossary.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<GlossaryEntry>>(json) ?? [];
        }
        return [];
    }

    public void SaveGlossary(List<GlossaryEntry> glossary)
    {
        EnsureConfigDir();
        File.WriteAllText(Path.Combine(_configDir, "glossary.json"),
            JsonExportService.BuildGlossaryJson(glossary));
    }

    private void EnsureConfigDir() => Directory.CreateDirectory(_configDir);
}
