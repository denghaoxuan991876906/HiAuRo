using HiAuRo.ACR;
using HiAuRo.Command;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("发送命令", "发送聊天命令")]
[TriggerTypeName("TriggerAction_SendCommand")]
public sealed class TriggerAction_发送命令 : ITriggerAction
{
    public string Command { get; set; } = "";
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        OmenTools.OmenService.ChatManager.Instance().SendCommand(Command);
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddTextInput("Command", Command ?? "");
    }
}
