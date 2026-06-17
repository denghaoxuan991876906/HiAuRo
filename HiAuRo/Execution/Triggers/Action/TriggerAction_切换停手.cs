using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("切换停手", "切换ACR停手状态")]
[TriggerTypeName("TriggerActionSwitchStop")]
public sealed class TriggerAction_切换停手 : ITriggerAction
{
    public bool Stop { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        ExecutionAxis.Instance.SetPause(Stop);
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddCheckbox("Stop", Stop);
    }
}
