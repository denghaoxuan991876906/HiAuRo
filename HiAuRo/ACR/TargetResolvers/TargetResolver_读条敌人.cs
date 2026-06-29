using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.ACR.TargetResolvers;

/// <summary>
/// 选择正在读条的敌人目标
/// </summary>
public sealed class TargetResolver_读条敌人 : ITargetResolver
{
    private readonly uint? _spellId;
    private readonly bool _interruptOnly;

    /// <param name="spellId">指定读条技能 ID（null = 任意读条）</param>
    /// <param name="interruptOnly">仅选中可打断读条</param>
    public TargetResolver_读条敌人(uint? spellId = null, bool interruptOnly = false)
    {
        _spellId = spellId;
        _interruptOnly = interruptOnly;
    }

    public bool ResolveTarget(out IBattleChara agent)
    {
        agent = null!;

        foreach (var obj in Data.Objects.Enemies)
        {
            if (obj is not IBattleNPC npc) continue;
            if (!npc.IsCasting) continue;
            if (!npc.IsTargetable || npc.IsDead == true) continue;

            if (_spellId.HasValue && npc.CastActionID != _spellId.Value) continue;
            if (_interruptOnly && !npc.IsCastInterruptible) continue;

            agent = npc;
            return true;
        }

        return false;
    }
}
