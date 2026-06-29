namespace HiAuRo.ACR;

/// <summary>
/// HiAuRo 内置通用 QT 类型
/// </summary>
public enum BuiltinQt
{
    /// <summary>爆发</summary>
    Burst,
    /// <summary>爆发药</summary>
    Potion,
    /// <summary>停手</summary>
    Hold,
    /// <summary>自动减伤</summary>
    Mitigation,
    /// <summary>清空资源</summary>
    Dump,
    /// <summary>群体攻击</summary>
    AoE,
    /// <summary>击杀时间</summary>
    TTK,
}

/// <summary>
/// BuiltinQt 元数据映射 —— ID / 标签 / 默认值
/// </summary>
public static class BuiltinQtHelper
{
    /// <summary>获取内置 QT 字符串 ID（如 "__builtin_burst"）</summary>
    public static string GetId(this BuiltinQt type) => type switch
    {
        BuiltinQt.Burst => "__builtin_burst",
        BuiltinQt.Potion => "__builtin_potion",
        BuiltinQt.Hold => "__builtin_hold",
        BuiltinQt.Mitigation => "__builtin_mitigation",
        BuiltinQt.Dump => "__builtin_dump",
        BuiltinQt.AoE => "__builtin_aoe",
        BuiltinQt.TTK => "__builtin_ttk",
        _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>获取内置 QT 显示标签</summary>
    public static string GetLabel(this BuiltinQt type) => type switch
    {
        BuiltinQt.Burst => "爆发",
        BuiltinQt.Potion => "爆发药",
        BuiltinQt.Hold => "停手",
        BuiltinQt.Mitigation => "自动减伤",
        BuiltinQt.Dump => "清空资源",
        BuiltinQt.AoE => "AOE",
        BuiltinQt.TTK => "TTK",
        _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>获取内置 QT 默认开关值</summary>
    public static bool GetDefault(this BuiltinQt type) => type switch
    {
        BuiltinQt.Burst => true,
        BuiltinQt.Potion => false,
        BuiltinQt.Hold => false,
        BuiltinQt.Mitigation => true,
        BuiltinQt.Dump => false,
        BuiltinQt.AoE => true,
        BuiltinQt.TTK => false,
        _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
