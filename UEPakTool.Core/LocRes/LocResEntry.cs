namespace UEPakTool.Core.LocRes;

public class LocResEntry
{
    public string Namespace { get; set; } = "";
    public string Key { get; set; } = "";
    public uint SourceStringHash { get; set; }
    public string LocalizedString { get; set; } = "";
}
