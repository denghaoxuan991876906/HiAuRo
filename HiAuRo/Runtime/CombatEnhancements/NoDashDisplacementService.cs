using HiAuRo.Infrastructure;

namespace HiAuRo.Runtime.CombatEnhancements;

public sealed class NoDashDisplacementService
{
    public static bool ShouldSuppressDash(
        NoDashDisplacementFilterMode mode,
        HashSet<uint> listedActionIds,
        uint actionId)
    {
        var isListed = listedActionIds.Contains(actionId);
        return mode == NoDashDisplacementFilterMode.Blacklist ? !isListed : isListed;
    }

    public static bool TryAddActionId(List<uint> actionIds, uint actionId)
    {
        if (actionId == 0 || actionIds.Contains(actionId))
            return false;

        actionIds.Add(actionId);
        return true;
    }

    public static bool RemoveActionId(List<uint> actionIds, uint actionId)
        => actionIds.Remove(actionId);

    public void Init()
    {
    }

    public void SyncFromConfig(PluginConfig config)
    {
    }

    public void Dispose()
    {
    }
}
