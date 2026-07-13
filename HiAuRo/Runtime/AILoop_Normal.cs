using static HiAuRo.Data;

namespace HiAuRo.Runtime;

public sealed class AILoop_Normal : IAILoop
{
    private bool _lastInCombat;

    public bool Check() => true;

    public void Update(AIRunner runner)
    {
        var bd = AIRunner.BattleData;

        if (Data.Combat.InCombat)
        {
            if (!_lastInCombat)
                _lastInCombat = true;

            if (Data.Me.Object is { IsDead: true })
            {
                if (!bd.IsDead)
                {
                    bd.IsDead = true;
                    bd.Reset();
                }
                return;
            }
            bd.IsDead = false;
        }

        if (!Data.Combat.InCombat && _lastInCombat)
        {
            _lastInCombat = false;
            Clear(runner);
        }

        runner.StartCalSlot();
    }

    public void Clear(AIRunner runner)
    {
        AIRunner.BattleData.Reset();
        SpellActionTracker.Instance.Clear();
        CountDownHandler.Instance.Reset();
    }
}
