using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("经过时间", "检测战斗开始后经过的时间")]
[TriggerTypeName("TriggerCondAfterBattleStart")]
public sealed class TriggerCond_经过时间 : ITriggerCond
{
    public int TimeMs { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return ExecutionAxis.Instance.BattleTimeMs >= TimeMs;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("TimeMs", TimeMs);
    }
}
