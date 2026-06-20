using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using HiAuRo.Infrastructure;
using OmenTools;
using System.Numerics;

namespace HiAuRo.Runtime.CombatEnhancements;

public unsafe sealed class AnimationLockClampService
{
    private delegate void ActionManagerUpdateDelegate(ActionManager* actionManager);
    private delegate void ProcessPacketActionEffectDelegate(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targets);

    private Hook<ActionManagerUpdateDelegate>? _actionManagerUpdateHook;
    private Hook<ProcessPacketActionEffectDelegate>? _processPacketActionEffectHook;
    private volatile bool _enabled;
    private volatile float _clampValue;

    public void Init()
    {
        _actionManagerUpdateHook ??= DService.Instance().Hook.HookFromAddress<ActionManagerUpdateDelegate>(
            (nint)ActionManager.Addresses.Update.Value,
            ActionManagerUpdateDetour);
        _processPacketActionEffectHook ??= DService.Instance().Hook.HookFromAddress<ProcessPacketActionEffectDelegate>(
            (nint)ActionEffectHandler.Addresses.Receive.Value,
            ProcessPacketActionEffectDetour);
    }

    public void SyncFromConfig(PluginConfig config)
    {
        _clampValue = Math.Max(0f, config.AnimationLockClampSeconds);

        if (config.EnableAnimationLockClamp)
            Enable();
        else
            Disable();
    }

    public void Dispose()
    {
        Disable();
        _processPacketActionEffectHook?.Dispose();
        _processPacketActionEffectHook = null;
        _actionManagerUpdateHook?.Dispose();
        _actionManagerUpdateHook = null;
    }

    private void Enable()
    {
        if (_enabled)
            return;

        _actionManagerUpdateHook?.Enable();
        _processPacketActionEffectHook?.Enable();
        _enabled = true;
    }

    private void Disable()
    {
        if (!_enabled)
            return;

        _processPacketActionEffectHook?.Disable();
        _actionManagerUpdateHook?.Disable();
        _enabled = false;
    }

    private void ActionManagerUpdateDetour(ActionManager* actionManager)
    {
        _actionManagerUpdateHook!.Original(actionManager);
        ClampAnimationLock(actionManager);
    }

    private void ProcessPacketActionEffectDetour(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targets)
    {
        _processPacketActionEffectHook!.Original(casterEntityId, caster, targetPos, header, effects, targets);
        ClampAnimationLock(ActionManager.Instance());
    }

    private void ClampAnimationLock(ActionManager* actionManager)
    {
        if (!_enabled || actionManager == null)
            return;

        actionManager->AnimationLock = MathF.Min(actionManager->AnimationLock, _clampValue);
    }
}
