using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("设置Rotation", "切换当前Rotation到指定职业")]
[TriggerTypeName("TriggerActionSetRotation")]
public sealed class TriggerAction_设置Rotation : ITriggerAction
{
    public uint TargetJobId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        return false;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("TargetJobId", (int)TargetJobId);
    }
}
