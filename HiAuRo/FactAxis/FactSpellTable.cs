using HiAuRo.ACR;

namespace HiAuRo.FactAxis;

/// <summary>
/// 事实轴技能执行数据表 — 独立于 DecisionSkillRegistry
/// 存"技能怎么放"（TargetType/类别/类型），DecisionEngine 执行时构造完整 Spell
/// </summary>
public static class FactSpellTable
{
    private static readonly Dictionary<uint, SpellExecutionInfo> _table = new();

    /// <summary>注册技能执行信息</summary>
    public static void 注册(uint id, string name,
        SpellTargetType target = SpellTargetType.Self,
        SpellCategory category = SpellCategory.Default,
        SpellType type = SpellType.Ability)
    {
        _table[id] = new SpellExecutionInfo
        {
            Id = id,
            Name = name,
            TargetType = target,
            SpellCategory = category,
            Type = type
        };
    }

    /// <summary>根据技能 ID 构造 Spell 对象</summary>
    public static Spell? 构造Spell(uint id)
    {
        if (!_table.TryGetValue(id, out var info)) return null;
        return new Spell
        {
            Id = info.Id,
            Name = info.Name,
            TargetType = info.TargetType,
            SpellCategory = info.SpellCategory,
            Type = info.Type
        };
    }

    /// <summary>清空技能表</summary>
    public static void Clear() => _table.Clear();
}

/// <summary>技能执行信息记录</summary>
public sealed record SpellExecutionInfo
{
    /// <summary>技能 ID</summary>
    public uint Id { get; init; }
    /// <summary>技能名称</summary>
    public string Name { get; init; } = "";
    /// <summary>目标类型</summary>
    public SpellTargetType TargetType { get; init; } = SpellTargetType.Self;
    /// <summary>技能分类</summary>
    public SpellCategory SpellCategory { get; init; } = SpellCategory.Default;
    /// <summary>技能类型</summary>
    public SpellType Type { get; init; } = SpellType.Ability;
}
