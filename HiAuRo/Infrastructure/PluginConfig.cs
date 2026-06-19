using HiAuRo.ACR;

namespace HiAuRo.Infrastructure;

/// <summary>UI 渲染模式</summary>
/// <summary>UI 渲染模式</summary>
public enum UIMode
{
    /// <summary>WebUI 模式</summary>
    WebUI = 0,
    /// <summary>ImGui 模式</summary>
    ImGui = 1
}

/// <summary>ImGui 主题模式</summary>
/// <summary>ImGui 主题模式</summary>
public enum ImGuiThemeMode
{
    /// <summary>亮色主题</summary>
    Light = 0,
    /// <summary>暗色主题</summary>
    Dark = 1
}

/// <summary>背景特效模式</summary>
public enum BgEffectMode
{
    /// <summary>无特效</summary>
    None = 0,
    /// <summary>星云（渐变+粒子+波纹+点击涟漪）</summary>
    Nebula = 1,
    /// <summary>代码雨（Matrix风格绿色字符飘落）</summary>
    MatrixRain = 2,
    /// <summary>几何光效（动态霓虹几何线条）</summary>
    GeometricGlow = 3,
    /// <summary>萤火虫</summary>
    Firefly = 4,
    /// <summary>星座连线</summary>
    Constellation = 5,
    /// <summary>黑魔纹</summary>
    LeyLines = 6,
}

/// <summary>
/// HiAuRo 主配置对象 —— 走 Dalamud 原生 IPluginConfiguration 序列化
/// </summary>
public sealed class PluginConfig : SettingsBase
{
    /// <summary>全局配置实例</summary>
    public static PluginConfig Instance { get; internal set; } = null!;

    /// <summary>测试/非插件初始化场景下确保存在默认实例</summary>
    internal static PluginConfig EnsureInstance()
    {
        Instance ??= new PluginConfig();
        return Instance;
    }

    /// <summary>配置版本</summary>
    public int Version { get; set; } = 1;
    /// <summary>是否启用 Debug 日志</summary>
    public bool DebugEnabled { get; set; }
    /// <summary>上次使用的插件版本</summary>
    public string? LastSeenPluginVersion { get; set; }
    /// <summary>插件加载次数</summary>
    public int LoadCount { get; set; }

    /// <summary>技能队列窗口 (ms)</summary>
    public int ActionQueueInMs { get; set; } = 400;

    /// <summary>GCD 内能力技最大次数</summary>
    public int MaxAbilityTimesInGcd { get; set; } = 2;

    /// <summary>连续两能力技之间的最小间隔 (ms)</summary>
    public int AbilityIntervalMs { get; set; } = 500;

    /// <summary>AOE 判定的敌人数</summary>
    public int AoeCount { get; set; } = 3;

    /// <summary>攻击距离</summary>
    public float AttackRange { get; set; } = 25f;

    /// <summary>UI 渲染模式</summary>
    public UIMode UIMode { get; set; } = UIMode.ImGui;

    /// <summary>ImGui 主题模式 (亮色/暗色)</summary>
    public ImGuiThemeMode ImGuiThemeMode { get; set; } = ImGuiThemeMode.Light;

    /// <summary>背景特效模式（默认关闭，避免战斗时装饰动画干扰读信息）</summary>
    public BgEffectMode BgEffect { get; set; } = BgEffectMode.None;

    /// <summary>是否启用开发者工具（调试/VFX/背景特效入口）。默认关闭，普通用户不可见。</summary>
    public bool ShowDeveloperTools { get; set; } = false;

    /// <summary>所有悬浮窗是否可见（一键开关）</summary>
    public bool AllOverlaysVisible { get; set; } = true;

    /// <summary>ImGui 模式 — StatusBar overlay X 位置</summary>
    public float OverlayStatusBarX { get; set; } = 100f;

    /// <summary>ImGui 模式 — StatusBar overlay Y 位置</summary>
    public float OverlayStatusBarY { get; set; } = 100f;

    /// <summary>ImGui 模式 — StatusBar 展开状态</summary>
    public bool OverlayStatusBarExpanded { get; set; } = true;

    /// <summary>ImGui 模式 — QT Panel overlay X 位置</summary>
    public float OverlayQtPanelX { get; set; } = 100f;

    /// <summary>ImGui 模式 — QT Panel overlay Y 位置</summary>
    public float OverlayQtPanelY { get; set; } = 300f;

    /// <summary>ImGui 模式 — Hotkey Panel overlay X 位置</summary>
    public float OverlayHotkeyPanelX { get; set; } = 100f;

    /// <summary>ImGui 模式 — Hotkey Panel overlay Y 位置</summary>
    public float OverlayHotkeyPanelY { get; set; } = 420f;

    public float OverlayStatusBarW { get; set; } = 400;
    public float OverlayStatusBarH { get; set; } = 320;
    public float OverlayStatusBarCollapsedW { get; set; } = 280;
    public float OverlayStatusBarCollapsedH { get; set; } = 80;
    public float OverlayQtPanelW { get; set; }
    public float OverlayQtPanelH { get; set; }
    public float OverlayHotkeyPanelW { get; set; }
    public float OverlayHotkeyPanelH { get; set; }

    /// <summary>QT 面板是否可见</summary>
    public bool QtPanelVisible { get; set; } = true;

    /// <summary>热键面板是否可见</summary>
    public bool HotkeyPanelVisible { get; set; } = true;

    /// <summary>CEF 悬浮窗设置</summary>
    public OverlayWindowSetting[] Overlays { get; set; } =
    [
        new() { Name = "MainWindow", Url = "http://localhost:5678/main.html", Width = 310, Height = 480 },
        new() { Name = "QtWindow", Url = "http://localhost:5678/qt.html", Width = 320, Height = 80 },
        new() { Name = "HotkeyWindow", Url = "http://localhost:5678/hotkey.html", Width = 320, Height = 100 },
    ];

    /// <summary>GitHub 个人访问令牌（repo 权限，用于上传触发器目录）</summary>
    public string? GitHubToken { get; set; }

    /// <summary>GitHub 仓库路径（owner/repo）</summary>
    public string CatalogRepo { get; set; } = "denghaoxuan991876906/CatalogData";

    /// <summary>GitHub 分支名</summary>
    public string CatalogBranch { get; set; } = "main";

    /// <summary>事实轴配置标记</summary>
    public FactAxisFlags FactAxis { get; set; } = new();
    /// <summary>自动切换模式</summary>
    public AutoSwitchMode AutoSwitch { get; set; } = AutoSwitchMode.Execution优先;

    /// <summary>执行轴 — 进副本自动加载时间轴</summary>
    public bool ExecutionAxisAutoLoad { get; set; } = true;
    /// <summary>辅助轴 — 进副本自动加载时间轴</summary>
    public bool AssistAxisAutoLoad { get; set; } = false;

    // ── 目标选择器 ──

    /// <summary>倒计时时自动选择目标（仅在副本生效）</summary>
    public bool TargetAutoSelectOnCountdown { get; set; }
    /// <summary>启用目标选择器</summary>
    public bool TargetSelectorEnabled { get; set; }
    /// <summary>选择逻辑</summary>
    public TargetSelectMode TargetSelectMode { get; set; } = TargetSelectMode.距离最近;
    /// <summary>索敌范围 (yalms)</summary>
    public float TargetSearchRange { get; set; } = 30f;
    /// <summary>有目标时不自动切换</summary>
    public bool TargetKeepCurrent { get; set; }
    /// <summary>死亡自动禁用</summary>
    public bool TargetDisableOnDeath { get; set; }
    /// <summary>优先选中带攻击头标的敌人</summary>
    public bool TargetPreferAggroMarked { get; set; }
    /// <summary>排除木人</summary>
    public bool TargetExcludeDummies { get; set; } = true;
    /// <summary>排除非敌对目标</summary>
    public bool TargetExcludeNonHostile { get; set; } = true;

    public AxisRole SelfAxisRole { get; set; } = AxisRole.D1;

    /// <summary>启用副本录制</summary>
    public bool RecordingEnabled { get; set; } = true;

    /// <summary>启用战斗操作录制器（事件采样型）。</summary>
    public bool CombatRecorderEnabled { get; set; }

    /// <summary>Combat Recorder 仅在战斗中写事件。</summary>
    public bool CombatRecorderOnlyInCombat { get; set; } = true;

    /// <summary>Combat Recorder 仅记录当前加载 ACR 对应职业。</summary>
    public bool CombatRecorderOnlyCurrentJob { get; set; } = true;

    /// <summary>Combat Recorder 每次技能事件额外写上一帧快照。</summary>
    public bool CombatRecorderIncludePreviousFrame { get; set; }

    /// <summary>Combat Recorder 是否记录自身/目标 Buff 明细。</summary>
    public bool CombatRecorderIncludeBuffDetails { get; set; } = true;

    /// <summary>Combat Recorder 是否记录当前 resolver Check 结果。</summary>
    public bool CombatRecorderIncludeResolverResults { get; set; }

    /// <summary>Combat Recorder source 字段由用户显式指定。</summary>
    public Recording.CombatRecorderSourceMode CombatRecorderSourceMode { get; set; } = Recording.CombatRecorderSourceMode.Acr;

    /// <summary>Combat Recorder 最近帧缓存轮询间隔。</summary>
    public int CombatRecorderSampleIntervalMs { get; set; } = 40;

    /// <summary>清理多少天以前的 Combat Recorder jsonl 日志。</summary>
    public int CombatRecorderKeepDays { get; set; } = 14;

    /// <summary>Combat Recorder 额外跟踪的技能列表。</summary>
    public List<CombatRecorderTrackedSkillConfig> CombatRecorderTrackedSkills { get; set; } = [];

    // ── 身位指示器 ──

    /// <summary>启用身位指示器 VFX</summary>
    public bool ShowPositional { get; set; } = true;
    /// <summary>显示目标可攻击范围圈</summary>
    public bool ShowTargetHitbox { get; set; } = true;
    /// <summary>显示最大自动攻击范围圈</summary>
    public bool ShowAutoAttackRange { get; set; }
}

/// <summary>CEF 悬浮窗设置</summary>
public sealed class OverlayWindowSetting
{
    /// <summary>窗口名称</summary>
    public string Name { get; set; } = "";
    /// <summary>URL</summary>
    public string Url { get; set; } = "";
    /// <summary>宽度</summary>
    public int Width { get; set; } = 640;
    /// <summary>高度</summary>
    public int Height { get; set; } = 480;
    /// <summary>缩放百分比</summary>
    public float Zoom { get; set; } = 100f;
    /// <summary>是否可见</summary>
    public bool Visible { get; set; } = true;
    /// <summary>是否锁定</summary>
    public bool Locked { get; set; } = true;
}

public sealed class CombatRecorderTrackedSkillConfig
{
    /// <summary>技能 ID。</summary>
    public uint ActionId { get; set; }
}

#region FactAxis

/// <summary>事实轴功能标记</summary>
public sealed class FactAxisFlags
{
    /// <summary>时间线观测</summary>
    public bool Observe { get; set; } = true;
    /// <summary>QT 调控</summary>
    public bool QtControl { get; set; }
    /// <summary>团队减伤分配</summary>
    public bool TeamMitigation { get; set; }
    /// <summary>单人减伤分配</summary>
    public bool PersonalMitigation { get; set; }
    /// <summary>团队治疗分配</summary>
    public bool TeamHealing { get; set; }
    /// <summary>技能强制释放</summary>
    public bool ForceExecute { get; set; }
    /// <summary>NavMesh 移动</summary>
    public bool MoveTo { get; set; }
    /// <summary>传送</summary>
    public bool TP { get; set; }
    /// <summary>站位保持</summary>
    public bool Hold { get; set; }
    /// <summary>移动模式</summary>
    public MovementMode MovementMode { get; set; } = MovementMode.NavMesh_TP兜底;
    /// <summary>移动需求触发模式：事实节点匹配 / 战斗时间</summary>
    public MovementTriggerMode MovementTriggerMode { get; set; } = MovementTriggerMode.FactNode;
}

/// <summary>移动模式</summary>
public enum MovementMode
{
    /// <summary>NavMesh 寻路</summary>
    NavMesh,
    /// <summary>直接传送</summary>
    TP,
    /// <summary>NavMesh 寻路 + TP 兜底</summary>
    NavMesh_TP兜底
}

/// <summary>移动需求触发模式</summary>
public enum MovementTriggerMode
{
    /// <summary>事实节点匹配（DemandBuffer 按 FactNodeId 匹配事实事件）</summary>
    FactNode,
    /// <summary>战斗时间（AddedOrder 作为目标战斗时间，按 TotalTime 触发）</summary>
    CombatTime,
}

/// <summary>目标选择逻辑</summary>
public enum TargetSelectMode
{
    /// <summary>当前血量百分比最高</summary>
    当前血量百分比最高,
    /// <summary>当前血量百分比最低</summary>
    当前血量百分比最低,
    /// <summary>当前血量数值最高</summary>
    当前血量数值最高,
    /// <summary>当前血量数值最低</summary>
    当前血量数值最低,
    /// <summary>总血量最高</summary>
    总血量最高,
    /// <summary>距离最近</summary>
    距离最近,
}

public enum AxisRole
{
    D1,
    D2,
    D3,
    D4,
    H1,
    H2,
    MT,
    ST
}

/// <summary>自动切换模式</summary>
public enum AutoSwitchMode
{
    /// <summary>不自动切换</summary>
    None,
    /// <summary>执行轴优先</summary>
    Execution优先,
    /// <summary>事实轴优先</summary>
    Fact优先
}

#endregion
