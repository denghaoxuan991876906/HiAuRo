using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测在回溯时间窗口内自身是否被指定连线过
/// </summary>
[TriggerDisplay("最近连线", "检测在指定时间窗口内自身是否被连线过")]
[TriggerTypeName("TriggerCondCheckRecentlyTether")]
public sealed class TriggerCond_最近连线 : ITriggerCond
{
    public uint TetherId { get; set; }
    public float CheckTimeSec { get; set; } = 3f;
    public string Remark { get; set; } = "";

    /// <summary>检测自身是否在时间窗口内被指定连线过</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var selfId = Me.Object?.EntityID ?? 0;
        if (selfId == 0) return false;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - (long)(CheckTimeSec * 1000);

        return BattleData.GetRecentTethers().Any(e =>
            e.TimestampMs >= cutoffMs &&
            e.TetherId == TetherId &&
            e.TargetId == selfId);
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("TetherId", (int)TetherId);
        builder.AddFloatInput("CheckTimeSec", CheckTimeSec);
    }
}
