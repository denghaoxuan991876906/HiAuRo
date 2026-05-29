namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者入口接口 —— 两种 UI 模式可选。
/// 如需宿主托管 Settings 持久化，同时实现 ISettingsProvider&lt;T&gt; 并让 Settings 类继承 AcrSettings。
/// </summary>
public interface IRotationEntry
{
    /// <summary>作者名</summary>
    string AuthorName { get; }

    /// <summary>ACR 适用场景类型（PvE / PvP / 通用）</summary>
    AcrType AcrType => AcrType.PvE;

    /// <summary>false=HiAuRo IUiBuilder / true=ACR 自带 HTML</summary>
    bool UseCustomUi { get; }

    /// <summary>settingFolder = ACR 配置目录 ({configDir}/ACR/{author}/)</summary>
    Rotation? Build(string settingFolder);

    /// <summary>UseCustomUi=true 时返回 null</summary>
    IRotationUI? GetRotationUI();

    /// <summary>可选设置页</summary>
    void OnDrawSetting();

    /// <summary>资源释放</summary>
    void Dispose();

    /// <summary>进入战斗循环时</summary>
    void OnEnterRotation();

    /// <summary>退出战斗循环时</summary>
    void OnExitRotation();

    /// <summary>支持的职业列表（用于 ACRLoader 注册）</summary>
    IEnumerable<Jobs> TargetJobs { get; }

    /// <summary>ACR 作者自定义 ImGui 窗口（不限数量），默认 null 表示无</summary>
    IEnumerable<ICustomWindow>? CustomWindows => null;
}
