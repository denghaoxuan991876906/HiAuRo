using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测在回溯时间窗口内是否出现无目标的技能效果（如 Boss 地面 AoE 反馈效果）
/// </summary>
[TriggerDisplay("无目标技能效果", "检测在指定时间窗口内是否出现无目标技能效果（地面 AoE 等）")]
[TriggerTypeName("TriggerCondReceviceNoTargetAbilityEffect")]
public sealed class TriggerCond_无目标技能效果 : ITriggerCond
{
    public uint ActionId { get; set; }
    public float CheckTimeSec { get; set; } = 3f;
    public string Remark { get; set; } = "";

    /// <summary>检测是否出现无目标技能效果</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is ReceviceNoTargetAbilityEffectCondParams nt)
            return nt.ActionId == ActionId;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - (long)(CheckTimeSec * 1000);

        return BattleData.GetRecentActionEffects().Any(e =>
            e.TimestampMs >= cutoffMs &&
            e.ActionId == ActionId);
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("ActionId", (int)ActionId);
        builder.AddFloatInput("CheckTimeSec", CheckTimeSec);
    }
}
