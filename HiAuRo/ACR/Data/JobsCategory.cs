namespace HiAuRo.ACR;

/// <summary>
/// 职业职能分类
/// </summary>
public enum JobsCategory
{
    Tank,
    Healer,
    Melee,
    Ranged,
    Caster
}

/// <summary>
/// 职业职能分类辅助方法
/// </summary>
public static class JobsCategoryHelper
{
    /// <summary>根据 ClassJob RowId 返回职业职能分类</summary>
    public static JobsCategory GetCategory(uint jobRowId) => jobRowId switch
    {
        19 or 21 or 32 or 37 => JobsCategory.Tank,
        24 or 28 or 33 or 40 => JobsCategory.Healer,
        20 or 22 or 30 or 34 or 39 or 41 => JobsCategory.Melee,
        23 or 31 or 38 => JobsCategory.Ranged,
        25 or 27 or 35 or 42 => JobsCategory.Caster,
        _ => JobsCategory.Caster
    };
}
