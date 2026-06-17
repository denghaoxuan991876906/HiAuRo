using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测指定技能的冷却剩余是否在阈值内
/// </summary>
[TriggerDisplay("技能冷却", "检测技能冷却剩余时间")]
[TriggerTypeName("TriggerCondCheckSpellCd")]
public sealed class TriggerCond_技能冷却 : ITriggerCond
{
    public uint SpellId { get; set; }
    public int RemainingMs { get; set; } = 500;
    public string Remark { get; set; } = "";

    /// <summary>检测技能冷却剩余是否在阈值内</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var cd = SpellHelper.GetCooldownRemaining(SpellId);
        return cd >= 0 && cd <= RemainingMs;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
        builder.AddIntInput("RemainingMs", RemainingMs);
    }
}
