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

    /// <summary>立即执行 Slot（跨帧模式，等待释放条件后执行）</summary>
    public static void Execute(Slot slot)
    {
        ACRLifecycle.Runner.SlotExecutor.StartSlot(slot);
    }
}
