using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("单位可选中", "检测指定DataId的单位是否可选中")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_单位可选中, HiAuRo")]

public sealed class TriggerCond_单位可选中 : ITriggerCond
{
    public uint DataId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
        {
            if (obj is IBattleNPC npc && npc.DataID == DataId && npc.IsTargetable)
                return true;
        }
        return false;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("DataId", (int)DataId);
    }
}
