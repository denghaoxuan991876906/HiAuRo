using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("连线", "检测是否存在指定连线")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_连线, HiAuRo")]

public sealed class TriggerCond_连线 : ITriggerCond
{
    public uint TetherId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is TetherCreateParams create)
            return create.TetherID == TetherId;

        if (condParams is TetherRemoveParams)
            return false;

        return BattleData.GetRecentTethers().Any(e => e.TetherId == TetherId);
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("TetherId", (int)TetherId);
    }
}
