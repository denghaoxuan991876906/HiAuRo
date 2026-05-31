using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// 统一 Slot 优先级调度栈。所有技能来源通过 Push 提交 Slot，ExecutionStage 统一 Pop 执行。
/// 每优先级保留最后一个 Push 的 Slot（覆盖），Pop 取最高优先级。每帧至多 Pop 一个。
/// </summary>
public sealed class PrioritySlotStack
{
    /// <summary>优先级枚举（值越小越高）</summary>
    public enum Priority
    {
        ExecAxis = 0,     // 执行轴强制
        AssistAxis = 1,   // 辅助轴强制
        FactAxis = 2,     // 事实轴减伤/治疗
        Opener = 3,       // 起手序列
        SpellQueue = 4,   // 热键排队
        AiLoop = 5,       // 正常循环
    }

    private readonly Slot?[] _slots = new Slot?[6];

    /// <summary>全局实例（static 访问器，任何位置可 Push）</summary>
    public static PrioritySlotStack Instance { get; } = new();

    /// <summary>是否为当帧首次 Push（用于 ProcessSpellQueue 等需要感知是否有更高优先级 Slot 的场景）</summary>
    public bool IsEmpty
    {
        get
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) return false;
            return true;
        }
    }

    /// <summary>提交 Slot。同优先级覆盖（后 Push 覆盖先 Push）。</summary>
    public void Push(Priority priority, Slot slot)
    {
        _slots[(int)priority] = slot;
    }

    /// <summary>取出最高优先级 Slot，取出后清除该槽位。无 Slot 返回 null。</summary>
    public Slot? Pop()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot != null)
            {
                _slots[i] = null;
                return slot;
            }
        }
        return null;
    }

    /// <summary>清空所有槽位（帧初调用）</summary>
    public void Clear()
    {
        Array.Clear(_slots);
    }
}
