using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.ACR.TargetResolvers;

/// <summary>
/// 以目标为中心，选择周围 AOE 范围内敌人最多的位置所对应的敌人
/// </summary>
public sealed class TargetResolver_最佳AOE位置 : ITargetResolver
{
    private readonly float _aoeRange;

    /// <param name="aoeRange">AOE 技能的溅射半径（yalms）</param>
    public TargetResolver_最佳AOE位置(float aoeRange = 5f)
    {
        _aoeRange = aoeRange;
    }

    public bool ResolveTarget(out IBattleChara agent)
    {
        agent = null!;

        if (Data.Objects.Enemies.Count == 0) return false;

        int bestCount = 0;
        IBattleChara? bestTarget = null;

        foreach (var obj in Data.Objects.Enemies)
        {
            if (obj is not IBattleChara center) continue;
            if (!center.IsTargetable || center.IsDead == true) continue;

            int count = 0;
            foreach (var other in Data.Objects.Enemies)
            {
                if (other is not IBattleChara bc2) continue;
                if (!bc2.IsTargetable || bc2.IsDead == true) continue;
                if (bc2.EntityID == center.EntityID) continue;

                // 计算 other 距离 center 的距离（用 2D 判定 AOE 范围）
                var dx = bc2.Position.X - center.Position.X;
                var dz = bc2.Position.Z - center.Position.Z;
                var dist = MathF.Sqrt(dx * dx + dz * dz);

                if (dist <= _aoeRange)
                    count++;
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestTarget = center;
            }
        }

        if (bestTarget == null) return false;
        agent = bestTarget;
        return true;
    }
}
