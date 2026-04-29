using System.Text;

namespace UEPakTool.Core.Ai;

public static class PromptBuilder
{
    public static string BuildSystemPrompt(List<(string Source, string Target)> glossary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是游戏本地化专家。将英文游戏文本翻译为简体中文。");
        sb.AppendLine();
        sb.AppendLine("规则：");
        sb.AppendLine("- 保持语气和风格一致");
        sb.AppendLine("- 保留格式标记：%d %s %.2f {0} {1} \\n <RichText>...</>");
        sb.AppendLine("- 不翻译变量名和代码标识符");
        sb.AppendLine("- 术语不确定时优先音译，保持一致性");
        sb.AppendLine("- 简洁自然，符合中文游戏习惯");

        if (glossary.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("术语表（必须使用）：");
            foreach (var (src, tgt) in glossary)
                sb.AppendLine($"  {src} → {tgt}");
        }

        sb.AppendLine();
        sb.AppendLine("输出纯 JSON 数组（不要 markdown 代码块）：");
        sb.AppendLine("""[{"k":"序号","t":"译文"},...]""");

        return sb.ToString();
    }

    public static string BuildUserPrompt(List<(string Key, string Original)> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("翻译以下文本：");
        sb.AppendLine();

        for (int i = 0; i < entries.Count; i++)
            sb.AppendLine($"{i}. {entries[i].Original}");

        sb.AppendLine();
        sb.AppendLine($"返回 JSON 数组（共 {entries.Count} 项），k 为序号字符串：");
        return sb.ToString();
    }
}
