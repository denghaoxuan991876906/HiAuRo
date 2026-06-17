using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("发送按键", "模拟按键操作")]
[TriggerTypeName("TriggerAction_SendKey")]
public sealed class TriggerAction_发送按键 : ITriggerAction
{
    public string Key { get; set; } = "";
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        HotkeyHelper.HandleKeyPress(Key);
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddTextInput("Key", Key ?? "");
    }
}
