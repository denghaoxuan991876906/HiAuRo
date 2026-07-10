namespace HiAuRo.ACR;

/// <summary>
/// Rotation 容器 —— 持有 ACR 所有子组件引用
/// </summary>
public sealed class Rotation
{
    // === MVP 必需字段 ===
    public List<SlotResolverData> SlotResolvers { get; set; } = [];
    public List<ISlotSequence> SlotSequences { get; set; } = [];
    public IOpener? Opener { get; set; }
    public IRotationEventHandler? EventHandler { get; set; }
    public List<ITriggerAction> TriggerActions { get; set; } = [];
    public List<ITriggerCond> TriggerConditions { get; set; } = [];

    public Jobs TargetJob { get; set; }
    public AcrType AcrType { get; set; }
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public string Description { get; set; } = string.Empty;

    public List<IHotkeyEventHandler> HotkeyEventHandlers { get; set; } = [];

    // === 可选字段 ===
    public Func<int>? CanPauseACRCheck { get; set; }

    // === Phase 6+ 预埋字段 ===
    /// <summary>高优先级技能插入合法性检查（执行轴强制技能前回调，返回 &lt;0 禁止）</summary>
    public Func<Slot?, int>? CanUseHighPrioritySlotCheck { get; set; }

    #region 链式调用方法

    public Rotation AddOpener(IOpener opener)
    {
        Opener = opener;
        return this;
    }

    public Rotation AddSlotSequences(params ISlotSequence[] seqs)
    {
        SlotSequences.AddRange(seqs);
        return this;
    }

    public Rotation AddTriggerAction(ITriggerAction action)
    {
        TriggerActions.Add(action);
        return this;
    }

    public Rotation AddTriggerCondition(ITriggerCond cond)
    {
        TriggerConditions.Add(cond);
        return this;
    }

    public Rotation AddHotkeyEventHandlers(params IHotkeyEventHandler[] handlers)
    {
        HotkeyEventHandlers.AddRange(handlers);
        return this;
    }

    public Rotation AddCanPauseACRCheck(Func<int> check)
    {
        CanPauseACRCheck = check;
        return this;
    }

    #endregion
}
