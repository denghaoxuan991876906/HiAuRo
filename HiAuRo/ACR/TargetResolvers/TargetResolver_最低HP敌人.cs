using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.ACR.TargetResolvers;

/// <summary>
/// 选择 HP 百分比最低的敌人目标
/// </summary>
public sealed class TargetResolver_最低HP敌人 : ITargetResolver
{
    private readonly float _maxHpThreshold; // 仅选 HP 低于此比例的

    /// <param name="maxHpThreshold">HP 阈值（0.0~1.0），只有低于此比例的目标才考虑</param>
    public TargetResolver_最低HP敌人(float maxHpThreshold = 1f)
    {
        _maxHpThreshold = maxHpThreshold;
    }

    public bool ResolveTarget(out IBattleChara agent)
    {
        agent = null!;

        float lowestPct = float.MaxValue;
        IBattleChara? lowest = null;

        foreach (var obj in Data.Objects.Enemies)
        {
            if (obj is not IBattleChara bc) continue;
            if (!bc.IsTargetable || bc.IsDead == true) continue;
            if (bc.MaxHp == 0) continue;

            float pct = (float)bc.CurrentHp / bc.MaxHp;
            if (pct > _maxHpThreshold) continue;

            if (pct < lowestPct)
            {
                lowestPct = pct;
                lowest = bc;
            }
        }

        if (lowest == null) return false;
        agent = lowest;
        return true;
    }
}
