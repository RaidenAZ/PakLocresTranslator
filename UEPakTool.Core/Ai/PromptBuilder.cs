using System.Text;

namespace UEPakTool.Core.Ai;

public static class PromptBuilder
{
    private const string JsonFormatInstruction =
        "输出纯 JSON 数组（不要 markdown 代码块）：\n" +
        "\"\"\"[{\"k\":\"序号\",\"t\":\"译文\"},...]\"\"\"";

    private const string DefaultTemplate =
        "你是游戏本地化专家。将英文游戏文本翻译为简体中文。\n" +
        "\n" +
        "规则：\n" +
        "- 保持语气和风格一致\n" +
        "- 保留格式标记：%d %s %.2f {0} {1} \\n <RichText>...</>\n" +
        "- 不翻译变量名和代码标识符\n" +
        "- 术语不确定时优先音译，保持一致性\n" +
        "- 简洁自然，符合中文游戏习惯\n" +
        "\n" +
        "{glossary}";

    /// <summary>返回内置默认模板（供 UI 展示和重置用）。</summary>
    public static string GetDefaultTemplate() => DefaultTemplate;

    /// <summary>
    /// 构建 System Prompt。
    /// 如果 customTemplate 非空则用模板替换占位符，否则用默认模板。
    /// JSON 格式指令由代码固定追加，不受用户模板影响。
    /// </summary>
    public static string BuildSystemPrompt(
        List<(string Source, string Target)> glossary,
        string? customTemplate = null)
    {
        var template = !string.IsNullOrWhiteSpace(customTemplate)
            ? customTemplate.Replace("\r\n", "\n")
            : DefaultTemplate;

        // 构建术语表文本
        string glossaryText = "";
        if (glossary.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("术语表（必须使用）：");
            foreach (var (src, tgt) in glossary)
                sb.AppendLine($"  {src} → {tgt}");
            glossaryText = sb.ToString().TrimEnd();
        }

        // 替换占位符
        var prompt = template.Replace("{glossary}", glossaryText);

        // 追加 JSON 格式指令（不受用户控制）
        return prompt.TrimEnd() + "\n\n" + JsonFormatInstruction;
    }

    /// <summary>构建 User Prompt，包含待翻译条目列表。</summary>
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
