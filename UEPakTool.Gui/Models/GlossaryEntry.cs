using CommunityToolkit.Mvvm.ComponentModel;

namespace UEPakTool.Gui.Models;

public partial class GlossaryEntry : ObservableObject
{
    [ObservableProperty]
    private string _source = "";

    [ObservableProperty]
    private string _target = "";

}
