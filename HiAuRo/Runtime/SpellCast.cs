using HiAuRo.ACR;
using OmenTools.OmenService;
using ActionTypeFF = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace HiAuRo.Runtime;

public static class SpellCast
{
    public static async Task<bool> ExecuteAsync(Slot slot, Spell spell, AIRunner runner)
    {
        if (Data.Me.Object is { IsDead: true }) return false;

        var spellId = spell.Id;
        var tracker = SpellActionTracker.Instance;

        if (tracker.HasFlag(spellId, SpellActionType.Effect))
        {
            tracker.Clear();
            return true;
        }

        if (tracker.HasFlag(spellId, SpellActionType.CancelCast)
            || tracker.HasFlag(spellId, SpellActionType.ActionRejected))
        {
            tracker.Clear();
        }

        tracker.BeginTrack(slot, spell);
        DService.Instance().Log.Debug($"[SpellCast] 开始: {spell.Name}({spellId}) IsAbility={spell.IsAbility()} CastTime={spell.CastTime.TotalMilliseconds:F0}ms");

        if (!spell.IsAbility())
        {
            long start = Environment.TickCount64;
            int ping = 10;
            int interval = 16;
            var queueMs = PluginConfig.Instance.ActionQueueInMs;
            while (GCDHelper.GetGCDCooldown() > ping + interval
                   && Environment.TickCount64 - start < queueMs)
            {
                if (tracker.HasFlag(spellId, SpellActionType.ActionRejected)
                    || tracker.HasFlag(spellId, SpellActionType.CancelCast))
                    return false;
                await Coroutine.Instance.WaitAsync(1);
            }
        }

        while (!GCDHelper.CanUseOffGcd())
        {
            if (tracker.HasFlag(spellId, SpellActionType.ActionRejected)
                || tracker.HasFlag(spellId, SpellActionType.CancelCast))
                return false;
            await Coroutine.Instance.WaitAsync(1);
        }

        var targetId = ResolveTarget(spell);
        long sendStart = Environment.TickCount64;
        bool accepted = false;

        while (Environment.TickCount64 - sendStart < 150
               && !tracker.HasFlag(spellId, SpellActionType.Request))
        {
            var useOk = UseActionManager.Instance().UseAction(ActionTypeFF.Action, spellId, targetId, 0, 0, 0);
            accepted = useOk || CheckQueued(spellId);
            if (accepted)
            {
                DService.Instance().Log.Debug($"[SpellCast] 队列确认: {spell.Name}({spellId}) useOk={useOk} tryMs={Environment.TickCount64 - sendStart}");
                break;
            }
            await Coroutine.Instance.WaitAsync(1);
        }

        if (!accepted)
        {
            DService.Instance().Log.Debug($"[SpellCast] 发送超时: {spell.Name}({spellId})");
            return false;
        }

        tracker.Notify(SpellActionType.Request, spellId);
        EventSystem.OnUseActionSuccess(spellId, spell.Type);

        long castTime = (long)spell.CastTime.TotalMilliseconds;
        DService.Instance().Log.Debug($"[SpellCast] 已入列: {spell.Name}({spellId}) castTime={castTime}ms");

        // 步骤 8：咏唱等待 + 条件检查
        // Effect 由 GameEventHook ActionEffect 钩子设置（真·服务端确认）
        if (castTime > 0)
        {
            // 8a: 等 500ms 等 Request/Effect 或 Cancel/Reject
            await Coroutine.Instance.WaitAsync(500,
                () => !tracker.HasFlag(spellId, SpellActionType.CancelCast)
                   && !tracker.HasFlag(spellId, SpellActionType.ActionRejected)
                   && !tracker.HasFlag(spellId, SpellActionType.Effect));
            if (tracker.HasFlag(spellId, SpellActionType.ActionRejected)) return false;

            // 8c: 确认 Request 或 Effect 已收到
            if (!tracker.HasFlag(spellId, SpellActionType.Request)
                && !tracker.HasFlag(spellId, SpellActionType.Effect))
                return false;

            // 8d: castTime - 300 = 剩余等待
            long remaining = Math.Max(0, castTime - 300);
            if (remaining > 0)
            {
                await Coroutine.Instance.WaitAsync(remaining,
                    () => !tracker.HasFlag(spellId, SpellActionType.CancelCast)
                       && !tracker.HasFlag(spellId, SpellActionType.ActionRejected)
                       && !tracker.HasFlag(spellId, SpellActionType.Effect));
            }
            if (tracker.HasFlag(spellId, SpellActionType.CancelCast)
                || tracker.HasFlag(spellId, SpellActionType.ActionRejected))
                return false;
        }
        else
        {
            // 瞬发：等 500ms 或 Request/Effect/ActionRejected
            await Coroutine.Instance.WaitAsync(500,
                () => !tracker.HasFlag(spellId, SpellActionType.Request)
                   && !tracker.HasFlag(spellId, SpellActionType.Effect)
                   && !tracker.HasFlag(spellId, SpellActionType.ActionRejected));
            if (tracker.HasFlag(spellId, SpellActionType.ActionRejected)) return false;
            if (!tracker.HasFlag(spellId, SpellActionType.Request)
                && !tracker.HasFlag(spellId, SpellActionType.Effect))
                return false;
        }

        // 等 Effect 服务器确认（步骤 9）
        // 能力技可跳过（WaitServerAcq=false）
        if (!tracker.HasFlag(spellId, SpellActionType.Effect)
            && (spell.IsAbility() && spell.WaitServerAcq || !spell.IsAbility()))
        {
            await Coroutine.Instance.WaitAsync(500,
                () => !tracker.HasFlag(spellId, SpellActionType.Effect)
                   && !tracker.HasFlag(spellId, SpellActionType.CancelCast)
                   && !tracker.HasFlag(spellId, SpellActionType.ActionRejected));
            if (tracker.HasFlag(spellId, SpellActionType.CancelCast)) return false;
            if (tracker.HasFlag(spellId, SpellActionType.ActionRejected)) return false;
            if (!tracker.HasFlag(spellId, SpellActionType.Effect)) return false;
        }

        tracker.Clear();

        while (!GCDHelper.CanUseOffGcd())
            await Coroutine.Instance.WaitAsync(1);

        runner.EventHandler?.OnSpellCastSuccess(slot, spell);
        DService.Instance().Log.Debug($"[SpellCast] 完成: {spell.Name}({spellId})");

        return true;
    }

    private static unsafe bool CheckQueued(uint spellId)
    {
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        return am != null && am->QueuedActionId == spellId;
    }

    private static ulong ResolveTarget(Spell spell)
    {
        var targetType = spell.TargetType;
        return targetType switch
        {
            SpellTargetType.Self => Data.Me.Object?.GameObjectID ?? 0,
            SpellTargetType.Target => Data.Target.Current?.GameObjectID ?? 0,
            SpellTargetType.TargetTarget => Data.Target.Current is IBattleChara bc ? bc.TargetObjectID : 0,
            SpellTargetType.SpecifyTarget => spell.SpecifyTarget is IGameObject go ? go.GameObjectID : 0,
            SpellTargetType.DynamicTarget => spell.GetDynamicTarget?.Invoke() is IGameObject dgo ? dgo.GameObjectID : 0,
            _ => Data.Target.Current?.GameObjectID ?? 0
        };
    }
}
