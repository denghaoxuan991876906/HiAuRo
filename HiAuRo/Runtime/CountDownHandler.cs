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

    private CountDownHandler() { }

    public void AddAction(int timeLeft, uint spellId, SpellTargetType targetType = SpellTargetType.Self)
    {
        _actions[timeLeft] = () => new Spell(spellId, targetType);
    }

    public void AddAction(int timeLeft, Func<Spell?> func)
    {
        _actions[timeLeft] = func;
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

        if (Start && remaining < 100)
        {
            CanDoAction = true;
            Start = false;
        }
    }

    internal async Task Init()
    {
        CanDoAction = false;
        Start = true;
        _done.Clear();
        _actions.Clear();
        CountDownStartTime = 0;
        await Task.CompletedTask;
    }

    internal void Reset()
    {
        Start = false;
        CanDoAction = false;
        _done.Clear();
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
