using Dalamud.Hooking;
using HiAuRo.Infrastructure;
using OmenTools;
using OmenTools.Interop.Game.Models;

namespace HiAuRo.Runtime.CombatEnhancements;

public sealed class SkillRangeExtensionService
{
    private static readonly CompSig ActionRangeSig =
        new("48 89 5C 24 ?? 57 48 83 EC ?? 48 8B 3D ?? ?? ?? ?? 8B D9 0F 29 74 24");

    private delegate float ActionRangeDelegate(uint actionId);

    private Hook<ActionRangeDelegate>? _actionRangeHook;
    private volatile bool _enabled;
    private volatile float _extensionValue;

    public void Init()
    {
        _actionRangeHook ??= ActionRangeSig.GetHook<ActionRangeDelegate>(ActionRangeDetour);
    }

    public void SyncFromConfig(PluginConfig config)
    {
        _extensionValue = Math.Max(0f, config.SkillRangeExtension);

        if (config.EnableSkillRangeExtension)
            Enable();
        else
            Disable();
    }

    public void Dispose()
    {
        Disable();
        _actionRangeHook?.Dispose();
        _actionRangeHook = null;
    }

    private void Enable()
    {
        if (_enabled)
            return;

        _actionRangeHook?.Enable();
        _enabled = true;
    }

    private void Disable()
    {
        if (!_enabled)
            return;

        _actionRangeHook?.Disable();
        _enabled = false;
    }

    private float ActionRangeDetour(uint actionId)
    {
        var originalRange = _actionRangeHook!.Original(actionId);
        if (originalRange <= 0f)
            return originalRange;

        return originalRange + _extensionValue;
    }
}
