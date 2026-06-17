using HiAuRo.ACR;

namespace HiAuRo.Decision;

#region 枚举

/// <summary>减伤适用类型</summary>
public enum 减伤类型 { 全能, 魔法, 物理 }

/// <summary>治疗方式</summary>
public enum 治疗类型 { 直疗, 持续, 直疗加盾 }

#endregion

#region 技能定义

/// <summary>团队减伤技能</summary>
public sealed class 团队减伤
{
    public uint 技能ID { get; init; }
    public string 名称 { get; init; } = "";
    public Jobs 职业 { get; init; }
    /// <summary>减伤百分比，10=10%</summary>
    public int 减伤百分比 { get; init; }
    public float 持续秒 { get; init; }
    public float 冷却秒 { get; init; }
    public 减伤类型 减伤类型 { get; init; }
    /// <summary>对应的状态 ID（buff/debuff），用于动态检查已生效的减伤</summary>
    public uint 状态ID { get; init; }
}

/// <summary>单人减伤技能（含盾，盾已换算为等效减伤%）</summary>
public sealed class 单人减伤
{
    public uint 技能ID { get; init; }
    public string 名称 { get; init; } = "";
    public Jobs 职业 { get; init; }
    public int 减伤百分比 { get; init; }
    public float 持续秒 { get; init; }
    public float 冷却秒 { get; init; }
    public 减伤类型 减伤类型 { get; init; }
    /// <summary>对应的状态 ID（buff），用于动态检查已生效的减伤</summary>
    public uint 状态ID { get; init; }
}

/// <summary>团队治疗技能</summary>
public sealed class 团队治疗
{
    public uint 技能ID { get; init; }
    public string 名称 { get; init; } = "";
    public Jobs 职业 { get; init; }
    public int 恢复力 { get; init; }
    public float 持续秒 { get; init; }
    public float 冷却秒 { get; init; }
    public bool 是否持续 { get; init; }
    public float 生效范围 { get; init; }
    public 治疗类型 治疗类型 { get; init; }
}

#endregion

#region 技能注册表

/// <summary>全部职业内置技能注册表（ACR 作者通过代码填充）</summary>
public static class DecisionSkillRegistry
{
    /// <summary>团队减伤: 职业 → 技能列表</summary>
    public static readonly Dictionary<Jobs, List<团队减伤>> 团队减伤表 = [];
    /// <summary>单人减伤: 职业 → 技能列表</summary>
    public static readonly Dictionary<Jobs, List<单人减伤>> 单人减伤表 = [];
    /// <summary>团队治疗: 职业 → 技能列表</summary>
    public static readonly Dictionary<Jobs, List<团队治疗>> 团队治疗表 = [];

    /// <summary>注册某个职业的技能</summary>
    public static void 注册(Jobs job, List<团队减伤>? teamMit = null, List<单人减伤>? personalMit = null, List<团队治疗>? teamHeal = null)
    {
        if (teamMit != null) 团队减伤表[job] = teamMit;
        if (personalMit != null) 单人减伤表[job] = personalMit;
        if (teamHeal != null) 团队治疗表[job] = teamHeal;
    }

    /// <summary>清空所有已注册技能</summary>
    public static void Clear()
    {
        团队减伤表.Clear();
        单人减伤表.Clear();
        团队治疗表.Clear();
    }
}

#endregion

#region 决策输出

/// <summary>减伤分配结果</summary>
public sealed class 减伤分配
{
    public uint 技能ID { get; set; }
    public string 技能名称 { get; set; } = "";
    public Jobs 职业 { get; set; }
    public int 减伤值 { get; set; }
    /// <summary>减伤持续秒</summary>
    public float 持续秒 { get; set; }
    public bool 团队减伤 { get; set; }
}

/// <summary>治疗分配结果</summary>
public sealed class 治疗分配
{
    public uint 技能ID { get; set; }
    public string 技能名称 { get; set; } = "";
    public Jobs 职业 { get; set; }
    public int 恢复力 { get; set; }
    public bool 是否持续 { get; set; }
}

/// <summary>决策引擎输出</summary>
public sealed class DecisionOutput
{
    public List<减伤分配> 减伤分配 { get; set; } = [];
    public List<治疗分配> 治疗分配 { get; set; } = [];
    public int 减伤合计 => 减伤分配.Sum(m => m.减伤值);
    public int 治疗合计 => 治疗分配.Sum(h => h.恢复力);
    public bool 满员 => !不足;

    /// <summary>是否已满足需求</summary>
    public bool 不足 { get; set; }
    /// <summary>本轮执行的技能 ID 列表（供 AIRunner 消费）</summary>
    public List<uint> 执行技能IDs { get; set; } = [];
}

#endregion
