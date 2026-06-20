using Dalamud.Hooking;
using HiAuRo.Infrastructure;
using OmenTools.Interop.Game.Models;

namespace HiAuRo.Runtime.CombatEnhancements;

public sealed class AntiKnockbackService
{
    private static readonly CompSig UnKnockbackSig =
        new("48 8B C4 57 48 81 EC ?? ?? ?? ?? 0F 29 70 ?? 0F 28 C1");

    private delegate long UnKnockbackDelegate(nint gameObject, float rotation, float length, nint a4, char a5, int a6);

    private Hook<UnKnockbackDelegate>? _unKnockbackHook;
    private volatile bool _enabled;

    public void Init()
    {
        _unKnockbackHook ??= UnKnockbackSig.GetHook<UnKnockbackDelegate>(UnKnockbackDetour);
    }

    public void SyncFromConfig(PluginConfig config)
    {
        if (config.EnableAntiKnockback)
            Enable();
        else
            Disable();
    }

    public void Dispose()
    {
        Disable();
        _unKnockbackHook?.Dispose();
        _unKnockbackHook = null;
    }

    private void Enable()
    {
        if (_enabled)
            return;

        _unKnockbackHook?.Enable();
        _enabled = true;
    }

    private void Disable()
    {
        if (!_enabled)
            return;

        _unKnockbackHook?.Disable();
        _enabled = false;
    }

    private long UnKnockbackDetour(nint gameObject, float rotation, float length, nint a4, char a5, int a6)
    {
        return _unKnockbackHook!.Original(gameObject, rotation, 0f, a4, a5, a6);
    }
}
