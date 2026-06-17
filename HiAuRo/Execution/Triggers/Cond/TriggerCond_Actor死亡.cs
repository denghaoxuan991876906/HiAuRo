using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("Actor死亡", "检测指定DataId的Actor死亡")]
[TriggerTypeName("TriggerCondActorDeath")]
public sealed class TriggerCond_Actor死亡 : ITriggerCond
{
    public uint DataId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
        {
            if (obj is IBattleNPC npc && npc.DataID == DataId)
            {
                if (npc.IsDead != true)
                    return false;
            }
        }
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("DataId", (int)DataId);
    }
}
