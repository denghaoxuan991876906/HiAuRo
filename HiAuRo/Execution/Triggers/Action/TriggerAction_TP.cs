using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("TP", "简易传送（HiAuRo 不做传送功能，此操作为占位）")]
[TriggerTypeName("TriggerAction_SimpleTP")]
public sealed class TriggerAction_TP : ITriggerAction
{
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        return true;
    }

    public void Draw(ACR.IUiBuilder builder) { }
}
