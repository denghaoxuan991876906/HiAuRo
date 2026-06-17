using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("切换自动攻击", "控制 ACR 是否主动拉怪/攻击（true=自动攻击, false=停手）")]
[TriggerTypeName("TriggerActionSwitchPull")]
public sealed class TriggerAction_切换自动攻击 : ITriggerAction
{
    public bool Enable { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        ExecutionAxis.Instance.IsPullEnabled = Enable;
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddCheckbox("Enable", Enable);
    }
}
