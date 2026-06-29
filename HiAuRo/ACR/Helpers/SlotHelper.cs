using HiAuRo.Runtime;

namespace HiAuRo.ACR;

/// <summary>
/// 在事件回调中手动将 Slot 加入执行队列。
/// 用于 OnGameEvent / OnBattleUpdate 等回调中需要立即响应的场景。
/// </summary>
public static class SlotHelper
{
    /// <summary>将 Slot 加入 SpellQueue，在下次队列处理时执行</summary>
    public static void Enqueue(Slot slot)
    {
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }

    /// <summary>将 Slot 直接压入 HighPrioritySlots 队列，由 ResolveSlots 第一时间处理</summary>
    public static void Execute(Slot slot)
    {
        bool isGcd = slot.Actions.Count > 0 && !slot.Actions[0].Spell.IsAbility();
        if (isGcd)
            AIRunner.BattleData.HighPrioritySlots_GCD.Enqueue(slot);
        else
            AIRunner.BattleData.HighPrioritySlots_OffGCD.Enqueue(slot);
    }
}
