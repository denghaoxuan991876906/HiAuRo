using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("等待目标", "等待指定DataId的目标出现")]
[TriggerTypeName("TriggerCondWaitTarget")]
public sealed class TriggerCond_等待目标 : ITriggerCond
{
    public uint DataId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.Enemies)
        {
            if (obj is IBattleNPC npc && npc.DataID == DataId && npc.IsTargetable && npc.IsDead != true)
                return true;
        }
        return false;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("DataId", (int)DataId);
    }
}
