namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者继承此类获得 Qt/Hotkey/Overlay UI 设置属性和 .Save() 方法
/// </summary>
public abstract class AcrSettings : SettingsBase
{
    public int QtCols { get; set; }
    public int QtBtnW { get; set; }
    public Dictionary<string, bool> QtVisible { get; set; } = [];
    public Dictionary<string, bool> QtValues { get; set; } = [];

    public int HkCols { get; set; }
    public int HkBtnSize { get; set; } = 52;
    public bool ShowHotkeyPanel { get; set; } = true;
    public Dictionary<string, bool> HkVisible { get; set; } = [];
    public Dictionary<string, string> HkBindings { get; set; } = [];

    public Dictionary<string, int> OverlayContentWidth { get; set; } = [];
    public Dictionary<string, int> OverlayContentHeight { get; set; } = [];
}
