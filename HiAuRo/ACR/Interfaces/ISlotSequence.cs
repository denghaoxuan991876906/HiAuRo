namespace HiAuRo.ACR;

/// <summary>
/// 技能序列接口 —— 支持两种实现模式
/// ACR 作者二选一实现：Sequence（Action&lt;Slot&gt; 委托）或 Resolvers（SlotResolverData 列表）
/// </summary>
public interface ISlotSequence
{
    /// <summary>HiAuRo 原生方式：Action&lt;Slot&gt; 委托构建序列</summary>
    List<Action<Slot>> Sequence { get; }

    /// <summary>SlotResolverData 有序列表（可选，二选一实现）</summary>
    List<SlotResolverData>? Resolvers => null;

    /// <summary>返回 >=0 表示可以启动序列</summary>
    int StartCheck();

    /// <summary>返回 >=0 表示可以在第 index 步中断</summary>
    int StopCheck(int index);
}
