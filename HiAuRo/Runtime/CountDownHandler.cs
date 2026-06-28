using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Runtime;

public sealed class CountDownHandler
{
    internal static CountDownHandler Instance { get; } = new();

    private readonly Dictionary<long, Func<Spell?>> _actions = new();
    private readonly HashSet<long> _done = new();
    internal bool Start { get; private set; }
    internal bool CanDoAction { get; private set; }
    internal long CountDownStartTime { get; set; }
    internal long LastPositiveRemaining { get; private set; }

    private CountDownHandler() { }

    public void AddAction(int timeLeft, uint spellId, SpellTargetType targetType = SpellTargetType.Self)
    {
        _actions[timeLeft] = () => new Spell(spellId, targetType);
    }

    public void AddAction(int timeLeft, Func<Spell?> func)
    {
        _actions[timeLeft] = func;
    }

    internal static CountdownProgressOutcome EvaluateProgressForTests(
        bool start,
        bool canDoAction,
        long remaining,
        long lastPositiveRemaining,
        bool inCombat,
        int registeredActions,
        int completedActions)
        => EvaluateProgress(start, canDoAction, remaining, lastPositiveRemaining, inCombat, registeredActions, completedActions);

    private static CountdownProgressOutcome EvaluateProgress(
        bool start,
        bool canDoAction,
        long remaining,
        long lastPositiveRemaining,
        bool inCombat,
        int registeredActions,
        int completedActions)
    {
        if (!start)
            return new CountdownProgressOutcome(start, canDoAction, CountdownStopReason.None);

        if (remaining <= 0)
        {
            // 取消倒计时时不放开正常战斗流；仅当已经进战时才把 0 视为自然结束/抢开结束。
            if (!inCombat && (lastPositiveRemaining <= 0 || lastPositiveRemaining > 100))
                return new CountdownProgressOutcome(false, false, CountdownStopReason.Canceled);

            return new CountdownProgressOutcome(false, true, CountdownStopReason.NaturalEnd);
        }

        if (remaining < 100)
            return new CountdownProgressOutcome(false, true, CountdownStopReason.NaturalEnd);

        if (registeredActions > 0 && registeredActions == completedActions && inCombat)
            return new CountdownProgressOutcome(false, true, CountdownStopReason.EarlyPull);

        return new CountdownProgressOutcome(start, canDoAction, CountdownStopReason.None);
    }

    internal async Task Update(BattleData battleData)
    {
        if (!Start) return;

        if (Me.Object is { IsDead: true })
        {
            Reset();
            return;
        }

        if (CountDownStartTime == 0)
        {
            if (ReadCountdown() == 0) return;
            CountDownStartTime = Environment.TickCount64;
        }

        long remaining = (long)(ReadCountdown() * 1000f);
        if (remaining > 0)
            LastPositiveRemaining = remaining;

        foreach (var kv in _actions)
        {
            if (_done.Contains(kv.Key)) continue;
            if (!Start) return;
            if (remaining > kv.Key) continue;

            _done.Add(kv.Key);
            try
            {
                var spell = kv.Value();
                if (spell != null)
                    battleData.AddSpell2NextSlot(spell);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[CountDown] {ex}");
            }
        }

        var outcome = EvaluateProgress(
            Start,
            CanDoAction,
            remaining,
            LastPositiveRemaining,
            Data.Combat.InCombat,
            _actions.Count,
            _done.Count);
        Start = outcome.Start;
        CanDoAction = outcome.CanDoAction;

        switch (outcome.StopReason)
        {
            case CountdownStopReason.NaturalEnd:
                break;
            case CountdownStopReason.EarlyPull:
                DService.Instance().Log.Information("[CountDown] 检测到抢开，倒计时动作已完成，提前放开正常战斗流");
                break;
            case CountdownStopReason.Canceled:
                DService.Instance().Log.Information("[CountDown] 检测到倒计时取消，已停止倒计时流程");
                break;
        }
    }

    internal async Task Init()
    {
        CanDoAction = false;
        Start = true;
        _done.Clear();
        _actions.Clear();
        CountDownStartTime = 0;
        LastPositiveRemaining = 0;
        await Task.CompletedTask;
    }

    internal void Reset()
    {
        Start = false;
        CanDoAction = false;
        _done.Clear();
        LastPositiveRemaining = 0;
    }

    internal void ClearActions()
    {
        _actions.Clear();
    }

    internal static unsafe float ReadCountdown()
    {
        var module = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentModule.Instance();
        if (module == null) return 0;
        var agent = module->GetAgentByInternalId(FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId.CountDownSettingDialog);
        if (agent == null) return 0;
        var countdown = (FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentCountDownSettingDialog*)agent;
        if (!countdown->Active) return 0;
        return countdown->TimeRemaining;
    }
}

internal enum CountdownStopReason
{
    None,
    NaturalEnd,
    EarlyPull,
    Canceled
}

internal readonly record struct CountdownProgressOutcome(
    bool Start,
    bool CanDoAction,
    CountdownStopReason StopReason);
