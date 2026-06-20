using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using HiAuRo.Infrastructure;
using LuminaAction = Lumina.Excel.Sheets.Action;
using OmenTools.Interop.Game.Models;
using OmenTools.OmenService;

namespace HiAuRo.Runtime.CombatEnhancements;

public unsafe sealed class NoDashDisplacementService
{
    private static readonly CompSig NoActionMoveSig =
        new("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B F1 0F 29 74 24 ?? 48 8B 89 ?? ?? ?? ?? 0F 28 F3");

    private delegate long NoActionMoveDelegate(long a1, byte a2, long a3, float a4, long a5);

    private readonly object _stateLock = new();
    private readonly HashSet<uint> _dashActionIds = [];
    private readonly HashSet<uint> _configuredActionIds = [];
    private readonly Queue<(uint ActionId, long Tick)> _pendingActions = new();

    private Hook<NoActionMoveDelegate>? _noActionMoveHook;
    private UseActionManager.PreUseActionDelegate? _preUseActionDelegate;
    private bool _enabled;
    private NoDashDisplacementFilterMode _mode = NoDashDisplacementFilterMode.Blacklist;

    private const int PendingActionRetentionMs = 1500;

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
        LoadDashActionIds();
        _noActionMoveHook ??= NoActionMoveSig.GetHook<NoActionMoveDelegate>(NoActionMoveDetour);
        _preUseActionDelegate ??= OnPreUseAction;
        UseActionManager.Instance().RegPreUseAction(_preUseActionDelegate);
    }

    public void SyncFromConfig(PluginConfig config)
    {
        lock (_stateLock)
        {
            _mode = config.NoDashDisplacementFilterMode;
            _configuredActionIds.Clear();
            foreach (var actionId in config.NoDashDisplacementActionIds)
                _configuredActionIds.Add(actionId);
        }

        if (config.EnableNoDashDisplacement)
            Enable();
        else
            Disable();
    }

    public void Dispose()
    {
        Disable();

        if (_preUseActionDelegate != null)
            UseActionManager.Instance().Unreg(_preUseActionDelegate);

        _preUseActionDelegate = null;
        _noActionMoveHook?.Dispose();
        _noActionMoveHook = null;

        lock (_stateLock)
        {
            _pendingActions.Clear();
            _configuredActionIds.Clear();
        }
    }

    private void Enable()
    {
        if (_enabled)
            return;

        _noActionMoveHook?.Enable();
        _enabled = true;
    }

    private void Disable()
    {
        if (!_enabled)
            return;

        _noActionMoveHook?.Disable();
        _enabled = false;

        lock (_stateLock)
            _pendingActions.Clear();
    }

    private void LoadDashActionIds()
    {
        if (_dashActionIds.Count > 0)
            return;

        var actionSheet = DService.Instance().Data.GetExcelSheet<LuminaAction>();
        if (actionSheet == null)
            return;

        foreach (var action in actionSheet.Where(a => a.AffectsPosition && a.CanTargetHostile && a.IsPlayerAction && !a.IsPvP))
            _dashActionIds.Add(action.RowId);
    }

    private void OnPreUseAction(
        ref bool isPrevented,
        ref ActionType actionType,
        ref uint actionId,
        ref ulong targetId,
        ref uint extraParam,
        ref ActionManager.UseActionMode queueState,
        ref uint comboRouteId)
    {
        if (!_enabled || isPrevented || actionType != ActionType.Action || actionId == 0)
            return;

        EnqueueIfDash(actionId);

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
            return;

        try
        {
            var adjustedActionId = actionManager->GetAdjustedActionId(actionId);
            if (adjustedActionId != 0 && adjustedActionId != actionId)
                EnqueueIfDash(adjustedActionId);
        }
        catch
        {
        }
    }

    private void EnqueueIfDash(uint actionId)
    {
        if (!_dashActionIds.Contains(actionId))
            return;

        lock (_stateLock)
        {
            CleanupPendingActions(Environment.TickCount64);
            _pendingActions.Enqueue((actionId, Environment.TickCount64));
        }
    }

    private long NoActionMoveDetour(long a1, byte a2, long a3, float a4, long a5)
    {
        lock (_stateLock)
        {
            CleanupPendingActions(Environment.TickCount64);
            if (_pendingActions.Count == 0)
                return _noActionMoveHook!.Original(a1, a2, a3, a4, a5);

            var pendingAction = _pendingActions.Dequeue().ActionId;
            if (ShouldSuppressDash(_mode, _configuredActionIds, pendingAction))
                return a5;
        }

        return _noActionMoveHook!.Original(a1, a2, a3, a4, a5);
    }

    private void CleanupPendingActions(long now)
    {
        while (_pendingActions.Count > 0)
        {
            var pending = _pendingActions.Peek();
            if (now - pending.Tick <= PendingActionRetentionMs)
                break;

            _pendingActions.Dequeue();
        }
    }
}
