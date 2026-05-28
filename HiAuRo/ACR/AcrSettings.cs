using HiAuRo.Setting;

namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者继承此类获得 .Save() 方法和 QT/Hotkey UI 设置属性
/// 宿主自动回填 _author / _jobId
/// 所有设置统一存储在 {configDir}/setting/ACR/{author}/{author}-{jobName}.json
/// </summary>
public abstract class AcrSettings
{
    internal string? _author;
    internal uint _jobId;

    #region QT 布局设置（ACR 作者无需手动管理）

    /// <summary>QT 面板每行列数</summary>
    public int QtCols { get; set; }
    /// <summary>QT 按钮宽度</summary>
    public int QtBtnW { get; set; }
    /// <summary>QT 可见性</summary>
    public Dictionary<string, bool> QtVisible { get; set; } = [];
    /// <summary>QT 开关值</summary>
    public Dictionary<string, bool> QtValues { get; set; } = [];

    #endregion

    #region 热键布局设置

    /// <summary>热键面板每行列数</summary>
    public int HkCols { get; set; }
    /// <summary>热键按钮大小(px)</summary>
    public int HkBtnSize { get; set; } = 52;
    /// <summary>热键可见性</summary>
    public Dictionary<string, bool> HkVisible { get; set; } = [];
    /// <summary>热键绑定</summary>
    public Dictionary<string, string> HkBindings { get; set; } = [];

    #endregion

    #region Overlay 尺寸

    /// <summary>每个 overlay 上次自适应后的宽度</summary>
    public Dictionary<string, int> OverlayContentWidth { get; set; } = [];
    /// <summary>每个 overlay 上次自适应后的高度</summary>
    public Dictionary<string, int> OverlayContentHeight { get; set; } = [];

    #endregion

    /// <summary>立即将当前 settings 写入磁盘</summary>
    public void Save()
    {
        if (_author == null) return;
        SettingMgr.SaveAcrJobSetting(_author, _jobId, this);
    }
}
