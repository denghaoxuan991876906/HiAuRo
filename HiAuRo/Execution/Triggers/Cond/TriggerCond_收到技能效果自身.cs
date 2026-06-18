using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测自身在回溯时间窗口内是否收到过指定技能效果（基于历史记录而非事件触发）
/// </summary>
[TriggerDisplay("收到技能效果自身", "检测在指定时间窗口内自身是否收到过指定技能效果")]
[TriggerTypeName("TriggerCondCheckAbilityEffect")]
public sealed class TriggerCond_收到技能效果自身 : ITriggerCond
{
    public uint ActionId { get; set; }
    public float CheckTimeSec { get; set; } = 3f;
    public string Remark { get; set; } = "";

    /// <summary>检测自身是否在时间窗口内收到过指定技能效果</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is ReceviceAbilityEffectCondParams ae)
        {
            var selfId = Me.Object?.EntityID ?? 0;
            return ae.ActionId == ActionId && ae.TargetID == selfId;
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - (long)(CheckTimeSec * 1000);

        return BattleEvents.GetRecentActionEffects().Any(e =>
            e.TimestampMs >= cutoffMs &&
            e.ActionId == ActionId &&
            e.TargetId == (Me.Object?.EntityID ?? 0));
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("ActionId", (int)ActionId);
        builder.AddFloatInput("CheckTimeSec", CheckTimeSec);
    }
}
