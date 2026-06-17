using HiAuRo.ACR;
using HiAuRo.Execution.Events;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("游戏日志", "检测游戏日志消息匹配")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_游戏日志, HiAuRo")]

public sealed class TriggerCond_游戏日志 : ITriggerCond
{
    public string MessagePattern { get; set; } = "";
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (!string.IsNullOrEmpty(MessagePattern) &&
            condParams is GameLogCondParams chat &&
            chat.Log.Contains(MessagePattern, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddTextInput("MessagePattern", MessagePattern ?? "");
    }
}
