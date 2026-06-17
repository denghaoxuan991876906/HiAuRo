using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("天气变化", "检测天气是否变为指定ID")]
[TriggerTypeName("TriggerCondOnWeatherIdChanged")]
public sealed class TriggerCond_天气变化 : ITriggerCond
{
    public byte WeatherId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return OmenTools.OmenService.GameState.Weather == WeatherId;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("WeatherId", WeatherId);
    }
}
