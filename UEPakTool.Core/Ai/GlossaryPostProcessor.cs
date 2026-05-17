using System.Text.RegularExpressions;

namespace UEPakTool.Core.Ai;

public static class GlossaryPostProcessor
{
    public static string ApplySingle(string translated,
        List<(string Source, string Target)> glossary)
    {
        foreach (var (source, target) in glossary)
        {
            translated = Regex.Replace(translated,
                Regex.Escape(source), target, RegexOptions.IgnoreCase);
        }
        return translated;
    }
}
