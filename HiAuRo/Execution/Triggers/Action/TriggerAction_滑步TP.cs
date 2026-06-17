using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("滑步TP", "滑步传送（HiAuRo 不做传送功能，此操作为占位）")]
[TriggerTypeName("TriggerAction_OnCastingTP")]
public sealed class TriggerAction_滑步TP : ITriggerAction
{
    public int WaitTillTime { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("WaitTillTime", WaitTillTime);
    }
}
