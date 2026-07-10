namespace HiAuRo.ACR;

/// <summary>
/// 技能序列接口 —— 仅支持按顺序执行的 Action&lt;Slot&gt; Sequence 列表
/// </summary>
public interface ISlotSequence
{
    /// <summary>HiAuRo 原生方式：Action&lt;Slot&gt; 委托构建序列</summary>
    List<Action<Slot>> Sequence { get; }

    /// <summary>返回 >=0 表示可以启动序列</summary>
    int StartCheck();

    /// <summary>返回 >=0 表示可以在第 index 步中断</summary>
    int StopCheck(int index);
}
