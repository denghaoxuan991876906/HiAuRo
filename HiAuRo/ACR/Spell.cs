using System.Numerics;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// 技能定义
/// </summary>
public sealed partial class Spell
{
    public static readonly Spell Idle = new()
    {
        Id = 0,
        Name = "Idle",
        TargetType = SpellTargetType.Self,
        Type = SpellType.None
    };

    public uint Id { get; init; }
    private string _name = string.Empty;
    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(_name)) return _name;
            _name = SpellHelper.GetActionRow(Id)?.Name.ToString() ?? Id.ToString();
            return _name;
        }
        init => _name = value;
    }
    public SpellTargetType TargetType { get; init; } = SpellTargetType.Target;
    public SpellCategory SpellCategory { get; init; } = SpellCategory.Default;
    public SpellType Type { get; init; } = SpellType.RealGcd;

    public object? SpecifyTarget { get; init; }
    public Func<object?>? GetDynamicTarget { get; init; }
    public Vector3? UsePos { get; init; }
    public bool DontUseGcdOpt { get; set; }
    public bool WaitServerAcq { get; init; }

    /// <summary>道具是否 HQ</summary>
    public bool Hq { get; init; }

    /// <summary>目标过滤限制</summary>
    public SpellTargetLimit[]? TargetLimits { get; init; }

    /// <summary>根据 TargetType 返回合适的目标 ID（含 TargetLimits 过滤）</summary>
    public uint GetTarget()
    {
        uint candidate = ResolveTarget();
        if (candidate == 0 || TargetLimits == null || TargetLimits.Length == 0)
            return candidate;

        // 验证候选目标是否通过所有过滤
        var candidateObj = GetObjectById(candidate);
        if (candidateObj != null && TargetLimits.All(l => l.Pass(candidateObj)))
            return candidate;

        // 候选不通过 → 搜索备选目标
        var searchPool = GetSearchPool();
        foreach (var obj in searchPool)
        {
            if (obj.EntityID == candidate) continue;
            if (TargetLimits.All(l => l.Pass(obj)))
                return obj.EntityID;
        }

        return 0; // 无符合条件的目标
    }

    /// <summary>根据 TargetType 解析基础目标（不含过滤）</summary>
    private uint ResolveTarget()
    {
        switch (TargetType)
        {
            case SpellTargetType.Self:
                return Data.Me.Object?.EntityID ?? 0;
            case SpellTargetType.Target:
                return Data.Target.Current?.EntityID ?? 0;
            case SpellTargetType.TargetTarget:
                if (Data.Target.Current is IBattleChara bc)
                    return (uint)bc.TargetObjectID;
                return 0;
            case SpellTargetType.SpecifyTarget:
                if (SpecifyTarget is IGameObject go)
                    return go.EntityID;
                return 0;
            case SpellTargetType.DynamicTarget:
                if (GetDynamicTarget?.Invoke() is IGameObject dgo)
                    return dgo.EntityID;
                return 0;
            case SpellTargetType.Pm1: case SpellTargetType.Pm2:
            case SpellTargetType.Pm3: case SpellTargetType.Pm4:
            case SpellTargetType.Pm5: case SpellTargetType.Pm6:
            case SpellTargetType.Pm7: case SpellTargetType.Pm8:
                var idx = (int)TargetType - (int)SpellTargetType.Pm1;
                if (idx >= 0 && idx < Data.Party.All.Count)
                    return Data.Party.All[idx].Player?.EntityID ?? 0;
                return 0;
            default:
                return Data.Target.Current?.EntityID ?? 0;
        }
    }

    /// <summary>按 EntityID 查找对象</summary>
    private static IGameObject? GetObjectById(uint entityId)
    {
        return entityId == 0 ? null : HiAuRo.Data.Objects.GetById(entityId) as IGameObject;
    }

    /// <summary>根据 TargetType 确定搜索池（过滤候选不通过时回退搜索）</summary>
    private IReadOnlyList<IGameObject> GetSearchPool()
    {
        return TargetType switch
        {
            SpellTargetType.Target or SpellTargetType.SpecifyTarget or SpellTargetType.DynamicTarget
                => Data.Objects.Enemies,
            SpellTargetType.Pm1 or SpellTargetType.Pm2 or SpellTargetType.Pm3 or SpellTargetType.Pm4
            or SpellTargetType.Pm5 or SpellTargetType.Pm6 or SpellTargetType.Pm7 or SpellTargetType.Pm8
                => Data.Party.All.Select(p => (p.Player as IGameObject)!).Where(o => o != null).ToList(),
            _ => []
        };
    }
}

public static class SpellExtensions
{
    /// <summary>是否为能力技——通过 recast group 判断是否与 GCD 共享计时器，不依赖 SpellType</summary>
    public static unsafe bool IsAbility(this Spell spell)
    {
        if (spell.Id == 0) return false;

        // 非战斗动作不触发 GCD，直接视为能力技
        if (spell.SpellCategory is SpellCategory.Sprint or SpellCategory.Potion or SpellCategory.Item or SpellCategory.LimitBreak)
            return true;

        var am = ActionManager.Instance();
        if (am == null) return false;

        // 用初始攻击(Action ID=9)获取 GCD recast group 作为基准
        var gcdGroup = am->GetRecastGroup((int)ActionType.Action, 9);
        var spellGroup = am->GetRecastGroup((int)ActionType.Action, spell.Id);

        return spellGroup != 0 && spellGroup != gcdGroup;
    }
}
