using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("移动到", "移动到指定位置（HiAuRo 设计不做自动跑位，此操作为占位）")]
[TriggerTypeName("TriggerAction_MoveTo")]
public sealed class TriggerAction_移动到 : ITriggerAction
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddFloatInput("X", X);
        builder.AddFloatInput("Y", Y);
        builder.AddFloatInput("Z", Z);
    }
}
