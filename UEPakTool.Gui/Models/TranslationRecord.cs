using CommunityToolkit.Mvvm.ComponentModel;

namespace UEPakTool.Gui.Models;

public enum TranslationSource
{
    Empty,
    OriginalLocale,   // locres 已有值，未被用户修改
    AiGenerated,
    ManuallyEntered,
    ManuallyEdited
}

public partial class TranslationRecord : ObservableObject
{
    public string Namespace { get; init; } = "";
    public string Key { get; init; } = "";
    public string DisplayKey => string.IsNullOrEmpty(Namespace) ? Key : $"{Namespace}::{Key}";

    public string Original { get; init; } = "";

    [ObservableProperty]
    private string _translated = "";

    [ObservableProperty]
    private TranslationSource _source = TranslationSource.Empty;

    public uint SourceStringHash { get; init; }

    /// <summary>
    /// 加载时设置 locres 已有值，不触发用户修改标记。
    /// </summary>
    #pragma warning disable MVVMTK0034
    public void SetInitialValue(string value)
    {
        _translated = value;
        _source = value.Length > 0 ? TranslationSource.OriginalLocale : TranslationSource.Empty;
    }
    #pragma warning restore MVVMTK0034

    partial void OnTranslatedChanged(string value)
    {
        if (Source == TranslationSource.AiGenerated)
            Source = TranslationSource.ManuallyEdited;
        else if (Source is TranslationSource.Empty or TranslationSource.OriginalLocale
                 && !string.IsNullOrWhiteSpace(value))
            Source = TranslationSource.ManuallyEntered;
        else if (Source != TranslationSource.Empty && string.IsNullOrWhiteSpace(value))
            Source = TranslationSource.Empty;
    }

    public bool IsTranslated => !string.IsNullOrWhiteSpace(Translated);
}
