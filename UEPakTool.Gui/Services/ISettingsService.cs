using UEPakTool.Gui.Models;

namespace UEPakTool.Gui.Services;

public interface ISettingsService
{
    AiConfig LoadAiConfig();
    void SaveAiConfig(AiConfig config);
    List<GlossaryEntry> LoadGlossary();
    void SaveGlossary(List<GlossaryEntry> glossary);
}
