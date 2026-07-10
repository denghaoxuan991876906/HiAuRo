namespace HiAuRo.ACR;

/// <summary>
/// 战斗事件回调处理接口 —— 11 个回调，除 OnBeforeSpellCast 外均必须显式实现
/// OnEnterRotation/OnExitRotation 已在 IRotationEntry 上定义，此处不重复
/// 由 AIRunner 在 Slot 执行过程中同步调用（主线程）
/// OnBeforeSpellCast 提供默认空实现；其他不需要的回调写空方法体
/// </summary>
public interface IRotationEventHandler
{
    /// <summary>非战斗情况下每帧触发（远敏唱歌、T切姿态等）</summary>
    void OnPreCombat();

    /// <summary>战斗重置时触发（团灭重来、脱战等）</summary>
    void OnResetBattle();

    /// <summary>没目标时触发（舞者转阶段提前跳舞等）</summary>
    void OnNoTarget();

    /// <summary>读条判定成功后（读条快结束、可滑步的时间点）</summary>
    void OnSpellCastSuccess(Slot slot, Spell spell);

    /// <summary>Slot 开始执行前触发。仅返回非 null 且不同于当前 Slot 时，才插入到当前 Slot 前面先执行。</summary>
    Slot? BeforeSpell(Slot slot);

    /// <summary>每个 Spell UseAction 之前触发。OnBeforeSpellCast(Slot, Spell) 通知回调。</summary>
    void OnBeforeSpellCast(Slot slot, Spell spell) { }

    /// <summary>技能使用后（DoT刷新后记录是否强化等）</summary>
    void AfterSpell(Slot slot, Spell spell);

    /// <summary>战斗中每帧触发（最常用的回调）</summary>
    void OnBattleUpdate(int battleTimeMs);

    /// <summary>切图时触发</summary>
    void OnTerritoryChanged();

    /// <summary>游戏事件分发回调。全部 ITriggerCondParams 子类型均转发，ACR 作者自行类型判断过滤。回调在 GameEventHook 线程执行，为只读通知。</summary>
    void OnGameEvent(ITriggerCondParams eventParams);

    /// <summary>事实轴阶段切换回调。仅 FactAxis 运行时触发。</summary>
    void OnPhaseChanged(string phaseId, string phaseName);
}
