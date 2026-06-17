using HiAuRo.ACR;
using static HiAuRo.Data;
using OmenTools.OmenService;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("切换目标", "切换当前目标到指定敌人")]
[TriggerTypeName("TriggerActionSelectenemy")]
public sealed class TriggerAction_切换目标 : ITriggerAction
{
    public uint TargetDataId { get; set; }
    public bool Nearest { get; set; } = true;
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        IBattleChara? target = null;
        var hasTargetDataId = TargetDataId != 0;

        if (hasTargetDataId)
        {
            foreach (var enemy in Objects.Enemies)
            {
                if (enemy is IBattleNPC npc && npc.DataID == TargetDataId)
                {
                    target = npc;
                    break;
                }
            }
        }
        else if (Nearest && Objects.Enemies.Count > 0)
        {
            float nearestDist = float.MaxValue;
            foreach (var enemy in Objects.Enemies)
            {
                if (enemy is not IBattleChara bc) continue;
                var dist = Data.Me.DistanceToObject3D(bc);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    target = bc;
                }
            }
        }

        if (target == null) return false;

        TargetManager.Target = target;
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("TargetDataId", (int)TargetDataId);
        builder.AddCheckbox("Nearest", Nearest);
    }
}
