using System.IO;
using System.Text;
using System.Text.Json;
using UEPakTool.Gui.Models;

namespace UEPakTool.Gui.Services;

public static class JsonExportService
{
    public static string BuildTranslationJson(IEnumerable<TranslationRecord> records)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, AppJsonOptions.IndentedWriter))
        {
            writer.WriteStartObject();
            foreach (var group in records
                .OrderBy(r => r.Namespace, StringComparer.Ordinal)
                .ThenBy(r => r.Key, StringComparer.Ordinal)
                .GroupBy(r => r.Namespace))
            {
                writer.WritePropertyName(group.Key);
                writer.WriteStartObject();
                foreach (var record in group)
                    writer.WriteString(record.Key, record.Translated ?? "");
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }

    public static string BuildGlossaryJson(IEnumerable<GlossaryEntry> entries) =>
        JsonSerializer.Serialize(
            entries
                .OrderBy(x => x.Source, StringComparer.Ordinal)
                .ThenBy(x => x.Target, StringComparer.Ordinal),
            AppJsonOptions.IndentedSerializer) + Environment.NewLine;

    public static string BuildAiConfigJson(AiConfig config) =>
        JsonSerializer.Serialize(config, AppJsonOptions.IndentedSerializer) + Environment.NewLine;
}
