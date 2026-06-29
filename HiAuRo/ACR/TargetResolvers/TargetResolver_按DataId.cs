using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.ACR.TargetResolvers;

/// <summary>
/// 按敌人 DataId (NPC ID) 选择目标
/// </summary>
public sealed class TargetResolver_按DataId : ITargetResolver
{
    private readonly uint _dataId;

    /// <param name="dataId">敌人 DataID（如 Boss 的 NPC ID）</param>
    public TargetResolver_按DataId(uint dataId)
    {
        _dataId = dataId;
    }

    public bool ResolveTarget(out IBattleChara agent)
    {
        agent = null!;

        foreach (var obj in Data.Objects.Enemies)
        {
            if (obj is not IBattleNPC npc) continue;
            if (!npc.IsTargetable) continue;
            if (npc.DataID != _dataId) continue;

            agent = npc;
            return true;
        }

        return false;
    }
}
