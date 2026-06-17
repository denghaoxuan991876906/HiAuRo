using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("单位移除", "检测指定DataId的单位是否移除")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_单位移除, HiAuRo")]

public sealed class TriggerCond_单位移除 : ITriggerCond
{
    public uint DataId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
        {
            if (obj is IBattleNPC npc && npc.DataID == DataId)
                return false;
        }
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("DataId", (int)DataId);
    }
}
