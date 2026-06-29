namespace HiAuRo.ACR;

/// <summary>
/// 技能目标类型 —— MVP 至少实现 Self/Target/TargetTarget
/// </summary>
public enum SpellTargetType
{
    Self,
    Target,
    TargetTarget,
    Pm1, Pm2, Pm3, Pm4, Pm5, Pm6, Pm7, Pm8,
    SpecifyTarget,
    Location,
    DynamicTarget,
    MapCenter
}
