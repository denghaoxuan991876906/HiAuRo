using HiAuRo.ACR;
using HiAuRo.UI;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ACRLifecycle ↔ ImGui overlay 窗口的状态通道
/// ACRLifecycle 写入，ImGui 窗口在 Draw() 中读取
/// </summary>
public static class ImGuiOverlayState
{
    /// <summary>是否运行中</summary>
    public static bool IsRunning;
    /// <summary>是否已暂停</summary>
    public static bool IsPaused;
    /// <summary>当前 ACR 名称</summary>
    public static string AcrName { get; set; } = "无ACR";

    /// <summary>ACR 声明的 UI 控件列表</summary>
    public static List<UiControlDef> Controls { get; set; } = [];

    /// <summary>当前 active tab ID</summary>
    public static string ActiveTab { get; set; } = string.Empty;

    /// <summary>ACR 持久化设置值（checkbox/slider/dropdown/intInput 的值）</summary>
    public static Dictionary<string, object> ControlValues { get; set; } = [];

    /// <summary>QT 芯片列表</summary>
    public static List<QtData> Qts { get; set; } = [];

    /// <summary>热键 resolver 列表</summary>
    public static List<IHotkeyResolver> Hotkeys { get; set; } = [];

    /// <summary>更新 ACR 状态（ACRLifecycle 调用）</summary>
    public static void UpdateStatus(string acrName, bool isRunning, bool isPaused,
        List<IHotkeyResolver> hotkeys, List<QtData> qts)
    {
        AcrName = acrName;
        IsRunning = isRunning;
        IsPaused = isPaused;
        Hotkeys = hotkeys;
        Qts = qts;
    }

    /// <summary>更新控件列表（ACRLifecycle 调用）</summary>
    public static void UpdateControls(List<UiControlDef> controls)
    {
        Controls = controls;
        if (controls.Count > 0)
        {
            ActiveTab = controls.FirstOrDefault(c => c.Type == "tab")?.Id ?? string.Empty;
        }
    }

    /// <summary>更新控件值（ACRLifecycle 调用）</summary>
    public static void UpdateControlValues(Dictionary<string, object> values)
    {
        ControlValues = values;
    }

    /// <summary>获取控件当前值</summary>
    public static T GetValue<T>(string id, T defaultValue)
    {
        if (ControlValues.TryGetValue(id, out var val) && val is T tv)
            return tv;
        return defaultValue;
    }

    /// <summary>设置控件值</summary>
    public static void SetValue(string id, object value)
    {
        ControlValues[id] = value;
    }
}
