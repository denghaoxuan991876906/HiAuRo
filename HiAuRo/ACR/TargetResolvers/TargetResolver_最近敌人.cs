using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.ACR.TargetResolvers;

/// <summary>
/// 选择最近的敌人目标
/// </summary>
public sealed class TargetResolver_最近敌人 : ITargetResolver
{
    public bool ResolveTarget(out IBattleChara agent)
    {
        agent = null!;

        var self = Data.Me.Object;
        if (self == null) return false;

        float nearestDist = float.MaxValue;
        IBattleChara? nearest = null;

        foreach (var obj in Data.Objects.Enemies)
        {
            if (obj is not IBattleChara bc) continue;
            if (!bc.IsTargetable || bc.IsDead == true) continue;

            var dist = Data.Me.DistanceToObject3D(bc, false);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = bc;
            }
        }

        if (nearest == null) return false;
        agent = nearest;
        return true;
    }
}
