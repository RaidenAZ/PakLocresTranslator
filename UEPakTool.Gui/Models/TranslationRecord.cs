using CommunityToolkit.Mvvm.ComponentModel;

namespace UEPakTool.Gui.Models;

public enum TranslationSource
{
    Empty,
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

    partial void OnTranslatedChanged(string value)
    {
        if (Source == TranslationSource.AiGenerated)
            Source = TranslationSource.ManuallyEdited;
        else if (Source == TranslationSource.Empty && !string.IsNullOrWhiteSpace(value))
            Source = TranslationSource.ManuallyEntered;
        else if (Source != TranslationSource.Empty && string.IsNullOrWhiteSpace(value))
            Source = TranslationSource.Empty;
    }

    public bool IsTranslated => !string.IsNullOrWhiteSpace(Translated);
}
