using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("地图特效", "检测地图特效触发")]
[TriggerTypeName("TriggerCondMapEffect")]
public sealed class TriggerCond_地图特效 : ITriggerCond
{
    public uint EffectId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is OnMapEffectCreateEvent me)
            return me.Pos == EffectId;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - 3000;
        return BattleData.GetRecentMapEffects().Any(e =>
            e.TimestampMs >= cutoffMs && e.EffectId == EffectId);
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("EffectId", (int)EffectId);
    }
}
