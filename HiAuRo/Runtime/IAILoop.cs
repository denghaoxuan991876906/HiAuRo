using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// AI 循环模式接口
/// </summary>
public interface IAILoop
{
    /// <summary>遍历所有 ISlotResolver 执行 Check()，结果存内部</summary>
    void CheckAll();

    /// <summary>基于 CheckAll 结果构建 Slot。blockBuild=true 时跳过 Build</summary>
    Slot? Build(bool blockBuild);

    /// <summary>[Obsolete] 向后兼容，内部委托到 CheckAll + Build</summary>
    [Obsolete("Use CheckAll() + Build() instead")]
    Slot? GetNextSlot(bool blockBuild = false);
}
