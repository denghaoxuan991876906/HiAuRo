using System.Text.Json.Serialization;

namespace HiAuRo.FactAxis;

/// <summary>事实轴事件类型——对应游戏包类型 (ITriggerCondParams 子类)</summary>
public enum FactEventType
{
    /// <summary>无游戏事件对照（纯时间标记/状态声明）</summary>
    None,
    /// <summary>技能效果 → ActionEffectParams</summary>
    Ability,
    /// <summary>读条开始 → ActorCastParams</summary>
    StartsUsing,
    /// <summary>点名标记 → ActorControlTargetIconParams</summary>
    HeadMarker,
    /// <summary>连线 → TetherCreateParams</summary>
    Tether,
    /// <summary>单位出现 → UnitCreateParams</summary>
    AddedCombatant,
    /// <summary>单位消失 → UnitDeleteParams</summary>
    RemovedCombatant,
    /// <summary>单位死亡 → ActorControlDeathParams</summary>
    WasDefeated,
    /// <summary>Buff获得 → BuffGainParams</summary>
    GainsEffect,
    /// <summary>Buff消失 → BuffRemoveParams</summary>
    LosesEffect,
    /// <summary>地图特效 → MapEffectParams</summary>
    MapEffect,
    /// <summary>NPC喊话 → NpcYellParams</summary>
    NPCYell,
    /// <summary>环境控制 → EnvControlParams</summary>
    EnvControl,
    /// <summary>天气变化 → WeatherChangedParams</summary>
    Weather,
    /// <summary>对象变化 → ObjectChangeParams</summary>
    ObjectChange,
    /// <summary>对象特效 → ObjectEffectParams</summary>
    ObjectEffect,
    /// <summary>对象生成 → ObjectSpawnParams</summary>
    ObjectSpawn,
}

#region 数据模型

/// <summary>副本时间线根</summary>
/// <summary>副本时间线根</summary>
public sealed class FactTimelineData
{
    /// <summary>时间线名称</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>副本 ID</summary>
    [JsonPropertyName("territoryId")]
    public uint TerritoryId { get; set; }

    /// <summary>作者</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    /// <summary>备注说明</summary>
    [JsonPropertyName("note")]
    public string Note { get; set; } = "";

    /// <summary>唯一标识（新建时自动生成）</summary>
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>阶段列表</summary>
    [JsonPropertyName("phases")]
    public List<FactPhase> Phases { get; set; } = [];
}

/// <summary>
/// 一个阶段 = 时间线列表 + 切换点
/// </summary>
/// <summary>一个阶段 = 时间线列表 + 切换点</summary>
public sealed class FactPhase
{
    /// <summary>阶段 ID</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>阶段名称</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>阶段内事件（纯时间推进）</summary>
    [JsonPropertyName("events")]
    public List<FactEvent> Events { get; set; } = [];

    /// <summary>阶段切换点 — Sync 触发后择分支，替换后续事件列表</summary>
    [JsonPropertyName("switch")]
    public FactPhaseSwitch? Switch { get; set; }
}

/// <summary>
/// 一个事件 — 阶段时间线上的一个 Boss 行为
/// </summary>
/// <summary>一个事件 — 阶段时间线上的一个 Boss 行为</summary>
public sealed class FactEvent
{
    /// <summary>事件 ID</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>事件名称</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>阶段内预期开始秒数</summary>
    [JsonPropertyName("time")]
    public double Time { get; set; }

    /// <summary>预期持续秒数（为 0 或 null = 瞬间事件）</summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>游戏事件类型。None=无游戏事件对照</summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FactEventType Type { get; set; } = FactEventType.None;

    /// <summary>主要ID（技能ID/连线ID/BuffID等，按Type解释）。0=无</summary>
    [JsonPropertyName("abilityId")]
    public uint AbilityId { get; set; }

    /// <summary>目标可选中状态声明。null=不涉及，true=变为可选中，false=变为不可选中</summary>
    [JsonPropertyName("targetable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Targetable { get; set; }

    /// <summary>开始校准 — 游戏事件匹配后记录实际开始时刻</summary>
    [JsonPropertyName("startSync")]
    public FactSyncDef? StartSync { get; set; }

    /// <summary>结束校准 — 游戏事件匹配后记录实际结束时刻</summary>
    [JsonPropertyName("endSync")]
    public FactSyncDef? EndSync { get; set; }

    /// <summary>事件触发时要执行的动作列表</summary>
    [JsonPropertyName("actions")]
    public List<FactAction> Actions { get; set; } = [];

    // 运行时
    /// <summary>是否已到达</summary>
    [JsonIgnore] public bool Reached { get; set; }
    /// <summary>实际开始时间</summary>
    [JsonIgnore] public double ActualStart { get; set; }
    /// <summary>实际结束时间</summary>
    [JsonIgnore] public double ActualEnd { get; set; }
    /// <summary>动作是否已执行</summary>
    [JsonIgnore] public bool ActionsDone { get; set; }
    /// <summary>Sync 是否已触发</summary>
    [JsonIgnore] public bool SyncFired { get; set; }

    /// <summary>向后兼容：若 Type=None 则尝试从 StartSync 迁移</summary>
    public void MigrateFromLegacy()
    {
        if (Type != FactEventType.None || AbilityId != 0) return;
        if (StartSync == null) return;

        Type = StartSync.Type switch
        {
            "ability"     => FactEventType.Ability,
            "startsUsing" => FactEventType.StartsUsing,
            _             => FactEventType.None
        };
        if (StartSync.AbilityIds is { Count: > 0 } ids)
            AbilityId = ids[0];

        // 清理旧字段以避免输出冗余
        StartSync.Type = null;
        StartSync.AbilityIds = null;
    }
}

/// <summary>
/// 阶段切换点 — 一个 Sync，匹配后择分支替换后续事件
/// </summary>
/// <summary>阶段切换点 — 一个 Sync，匹配后择分支替换后续事件</summary>
public sealed class FactPhaseSwitch
{
    /// <summary>同步定义</summary>
    [JsonPropertyName("sync")]
    public FactSyncDef Sync { get; set; } = new();

    /// <summary>切换时执行的动作</summary>
    [JsonPropertyName("actions")]
    public List<FactAction> Actions { get; set; } = [];

    /// <summary>分支列表 — 第一个条件满足的即为选中分支</summary>
    [JsonPropertyName("branches")]
    public List<FactSwitchBranch> Branches { get; set; } = [];
}

/// <summary>
/// 一个分支 — 满足条件后切到对应的事件列表 + 下一切换点
/// </summary>
/// <summary>一个分支 — 满足条件后切到对应的事件列表 + 下一切换点</summary>
public sealed class FactSwitchBranch
{
    /// <summary>分支名称</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>条件。null = 默认分支（无条件，兜底）</summary>
    [JsonPropertyName("condition")]
    public FactCondition? Condition { get; set; }

    /// <summary>分支选中后的事件列表</summary>
    [JsonPropertyName("events")]
    public List<FactEvent> Events { get; set; } = [];

    /// <summary>本分支结束时的下一切换点（null = 本分支就是阶段终点）</summary>
    [JsonPropertyName("switch")]
    public FactPhaseSwitch? Switch { get; set; }
}

#endregion

#region 同步 + 条件 + 动作

/// <summary>同步定义 — 将游戏事件校准到时间线</summary>
public sealed class FactSyncDef
{
    // 仅向后兼容反序列化，不参与新格式输出
    /// <summary>同步类型（向后兼容）</summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    /// <summary>技能 ID 列表（向后兼容）</summary>
    [JsonPropertyName("abilityIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<uint>? AbilityIds { get; set; }

    /// <summary>窗口提前打开秒数（默认 2.5）</summary>
    [JsonPropertyName("windowBefore")]
    public double WindowBefore { get; set; } = 2.5;

    /// <summary>窗口延后关闭秒数（默认 2.5）</summary>
    [JsonPropertyName("windowAfter")]
    public double WindowAfter { get; set; } = 2.5;

    /// <summary>同步命中后跳转到的目标时间（null = 不跳转）</summary>
    [JsonPropertyName("jump")]
    public double? Jump { get; set; }

    /// <summary>是否为无条件跳转（时间到即跳，不等 sync 匹配）</summary>
    [JsonPropertyName("forcejump")]
    public bool ForceJump { get; set; }

    /// <summary>无条件跳转的目标时间</summary>
    [JsonPropertyName("forcejumpTarget")]
    public double? ForceJumpTarget { get; set; }

    // === 运行时计算（不参与 JSON 序列化） ===
    /// <summary>窗口开始时间（运行时计算）</summary>
    [JsonIgnore] public double Start { get; set; }
    /// <summary>窗口结束时间（运行时计算）</summary>
    [JsonIgnore] public double End { get; set; }
    /// <summary>锚点时间（运行时计算）</summary>
    [JsonIgnore] public double AnchorTime { get; set; }
}

/// <summary>条件基类</summary>
public abstract class FactCondition
{
    /// <summary>评估条件是否满足</summary>
    public abstract bool Evaluate(Func<string, bool> lookup);
}

/// <summary>变量条件 — 检查变量是否等于期望值</summary>
public sealed class VariableCondition : FactCondition
{
    /// <summary>变量名</summary>
    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = "";
    /// <summary>期望值</summary>
    [JsonPropertyName("expectedValue")]
    public bool ExpectedValue { get; set; } = true;
    /// <summary>评估变量条件</summary>
    public override bool Evaluate(Func<string, bool> lookup) => lookup(VariableName) == ExpectedValue;
}

/// <summary>事实轴动作基类</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SetVariableAction), "setVariable")]
[JsonDerivedType(typeof(ToggleVariableAction), "toggleVariable")]
[JsonDerivedType(typeof(SkillSuggestionAction), "skillSuggestion")]
[JsonDerivedType(typeof(LogMessageAction), "logMessage")]
[JsonDerivedType(typeof(需求减伤动作), "需求减伤")]
[JsonDerivedType(typeof(需求治疗动作), "需求治疗")]
[JsonDerivedType(typeof(设置QT动作), "设置QT")]
[JsonDerivedType(typeof(切换QT动作), "切换QT")]
[JsonDerivedType(typeof(站位需求动作), "站位需求")]
public abstract class FactAction
{
    /// <summary>执行动作</summary>
    public abstract void Execute(FactTimeline timeline);
}

/// <summary>设置变量值动作</summary>
public sealed class SetVariableAction : FactAction
{
    /// <summary>变量名</summary>
    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = "";
    /// <summary>值</summary>
    [JsonPropertyName("value")]
    public bool Value { get; set; } = true;
    /// <summary>执行设置变量</summary>
    public override void Execute(FactTimeline timeline) => timeline.SetVariable(VariableName, Value);
}

/// <summary>切换变量值动作</summary>
public sealed class ToggleVariableAction : FactAction
{
    /// <summary>变量名</summary>
    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = "";
    /// <summary>执行切换变量</summary>
    public override void Execute(FactTimeline timeline) => timeline.ToggleVariable(VariableName);
}

/// <summary>技能建议动作</summary>
public sealed class SkillSuggestionAction : FactAction
{
    /// <summary>技能 ID</summary>
    [JsonPropertyName("skillId")] public uint SkillId { get; set; }
    /// <summary>标签</summary>
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    /// <summary>优先级</summary>
    [JsonPropertyName("priority")] public string Priority { get; set; } = "normal";
    /// <summary>执行技能建议</summary>
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>日志消息动作</summary>
public sealed class LogMessageAction : FactAction
{
    /// <summary>消息内容</summary>
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    /// <summary>执行日志输出</summary>
    public override void Execute(FactTimeline timeline) =>
        DService.Instance().Log.Information($"[FactAxis] {Message}");
}

/// <summary>需求动作 — 告知决策层此处需要多少减伤/治疗</summary>
[Obsolete("使用 需求减伤动作 / 需求治疗动作 替代")]
public sealed class 需求动作 : FactAction
{
    /// <summary>减伤需求</summary>
    [JsonPropertyName("需求减伤")]
    public int 需求减伤 { get; set; }

    /// <summary>治疗需求</summary>
    [JsonPropertyName("需求治疗")]
    public int 需求治疗 { get; set; }

    /// <summary>执行需求声明</summary>
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>减伤需求 — 事件到达时评估，在技能持续窗口内释放</summary>
public sealed class 需求减伤动作 : FactAction
{
    /// <summary>减伤需求值</summary>
    [JsonPropertyName("value")]
    public int Value { get; set; }
    /// <summary>执行减伤需求</summary>
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>治疗需求 — 事件到达时立即分配+释放</summary>
public sealed class 需求治疗动作 : FactAction
{
    /// <summary>治疗需求值</summary>
    [JsonPropertyName("value")]
    public int Value { get; set; }
    /// <summary>执行治疗需求</summary>
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>设置 QT — 到达时(或offset后)调 QTHelper.SetValue</summary>
public sealed class 设置QT动作 : FactAction
{
    /// <summary>QT ID</summary>
    [JsonPropertyName("qtId")]
    public string QtId { get; set; } = "";
    /// <summary>值</summary>
    [JsonPropertyName("value")]
    public bool Value { get; set; }
    /// <summary>偏移时间（秒）</summary>
    [JsonPropertyName("offset")]
    public double Offset { get; set; }
    /// <summary>执行设置 QT</summary>
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>切换 QT — 到达时(或offset后)调 QTHelper.Toggle</summary>
public sealed class 切换QT动作 : FactAction
{
    /// <summary>QT ID</summary>
    [JsonPropertyName("qtId")]
    public string QtId { get; set; } = "";
    /// <summary>偏移时间（秒）</summary>
    [JsonPropertyName("offset")]
    public double Offset { get; set; }
    /// <summary>执行切换 QT</summary>
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>站位需求 — 声明 deadline，位置由辅助轴通过 FactNodeId 关联</summary>
public sealed class 站位需求动作 : FactAction
{
    /// <summary>截止时间（秒）</summary>
    [JsonPropertyName("deadline")]
    public double Deadline { get; set; }
    /// <summary>角色分类</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "All";
    /// <summary>执行站位需求</summary>
    public override void Execute(FactTimeline timeline) { }
}

#endregion

#region 运行时状态

/// <summary>事实轴运行时状态</summary>
public sealed class FactState
{
    /// <summary>是否在运行</summary>
    public bool IsRunning { get; set; }
    /// <summary>时间线名称</summary>
    public string TimelineName { get; set; } = "";
    /// <summary>阶段名称</summary>
    public string PhaseName { get; set; } = "";
    /// <summary>阶段内时间（秒）</summary>
    public double PhaseTime { get; set; }
    /// <summary>总时间（秒）</summary>
    public double TotalTime { get; set; }
    /// <summary>当前事件</summary>
    public FactEvent? CurrentEvent { get; set; }
    /// <summary>状态标识</summary>
    public string Status { get; set; } = "";
    /// <summary>下一个事件时间</summary>
    public double? NextEventTime { get; set; }
    /// <summary>技能建议列表</summary>
    public List<SkillSuggestionAction> Suggestions { get; set; } = [];
    /// <summary>变量字典</summary>
    public Dictionary<string, bool> Variables { get; set; } = [];
    /// <summary>最后 Sync 信息</summary>
    public string LastSyncInfo { get; set; } = "";

    /// <summary>当前目标可选中状态。未声明时为 null。</summary>
    [JsonIgnore]
    public bool? IsTargetable { get; set; }

    /// <summary>距下次变为可占用的秒数。当前已可占用时返回 0，无后续声明时返回 null。</summary>
    [JsonIgnore]
    public double? NextTargetableIn =>
        IsTargetable == true ? 0 : PendingEvents.FirstOrDefault(e => e.Targetable == true)?.Time - PhaseTime;

    /// <summary>距下次变为不可占用的秒数。当前已不可占用时返回 0，无后续声明时返回 null。</summary>
    [JsonIgnore]
    public double? NextUntargetableIn =>
        IsTargetable == false ? 0 : PendingEvents.FirstOrDefault(e => e.Targetable == false)?.Time - PhaseTime;

    /// <summary>当前阶段未到达的事件（按时序）。ACR 可自定前向扫描。</summary>
    [JsonIgnore]
    public List<FactEvent> PendingEvents { get; set; } = [];

    /// <summary>查询距指定游戏事件类型的秒数。无匹配返回 null。</summary>
    public double? NextEventTimeOfType(FactEventType type) =>
        PendingEvents.FirstOrDefault(e => e.Type == type)?.Time - PhaseTime;

    /// <summary>查询距指定类型+技能ID的游戏的秒数。abilityId=0 时不筛ID。</summary>
    public double? NextEventTimeOfType(FactEventType type, uint abilityId) =>
        PendingEvents.FirstOrDefault(e => e.Type == type && (abilityId == 0 || e.AbilityId == abilityId))?.Time - PhaseTime;

    /// <summary>清空运行时状态</summary>
    public void Clear()
    {
        IsRunning = false; PhaseName = ""; PhaseTime = 0; TotalTime = 0;
        CurrentEvent = null; Status = ""; NextEventTime = null;
        Suggestions.Clear(); Variables.Clear(); LastSyncInfo = "";
        IsTargetable = null;
        PendingEvents.Clear();
    }
}

#endregion
