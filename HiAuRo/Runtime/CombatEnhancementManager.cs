using HiAuRo.Infrastructure;
using HiAuRo.Runtime.CombatEnhancements;

namespace HiAuRo.Runtime;

public sealed class CombatEnhancementManager
{
    public static CombatEnhancementManager Instance { get; } = new();

    private readonly SkillRangeExtensionService _range = new();
    private readonly AntiKnockbackService _knockback = new();
    private readonly NoDashDisplacementService _dash = new();
    private readonly AnimationLockClampService _animlock = new();
    private bool _initialized;

    public void Init()
    {
        if (_initialized)
            return;

        _range.Init();
        _knockback.Init();
        _dash.Init();
        _animlock.Init();
        _initialized = true;
        SyncFromConfig(PluginConfig.Instance);
    }

    public void SyncFromConfig(PluginConfig config)
    {
        _range.SyncFromConfig(config);
        _knockback.SyncFromConfig(config);
        _dash.SyncFromConfig(config);
        _animlock.SyncFromConfig(config);
    }

    public void Shutdown()
    {
        if (!_initialized)
            return;

        _animlock.Dispose();
        _dash.Dispose();
        _knockback.Dispose();
        _range.Dispose();
        _initialized = false;
    }
}
