using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("倒计时", "检测副本战斗倒计时剩余秒数")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_倒计时, HiAuRo")]

public sealed class TriggerCond_倒计时 : ITriggerCond
{
    public int TimeLeftSec { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        try
        {
            var remaining = AIRunner.ReadCountdown();
            if (remaining <= 0) return false;
            return Math.Abs(remaining - TimeLeftSec) <= 0.5f;
        }
        catch
        {
            return false;
        }
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("TimeLeftSec", TimeLeftSec);
    }
}
