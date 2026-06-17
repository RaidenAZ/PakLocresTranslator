using System.Text.Encodings.Web;
using System.Text.Json;

namespace UEPakTool.Gui.Services;

public static class AppJsonOptions
{
    public static readonly JsonSerializerOptions IndentedSerializer = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public static readonly JsonWriterOptions IndentedWriter = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
