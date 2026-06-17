using HiAuRo.ACR;
using HiAuRo.Execution.Events;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("收到技能效果", "检测是否收到指定技能效果")]
[TriggerTypeName("TriggerCondReceviceAbilityEffect")]
public sealed class TriggerCond_收到技能效果 : ITriggerCond
{
    public uint SpellId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is ReceviceAbilityEffectCondParams ae)
            return ae.ActionId == SpellId;
        return false;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
    }
}
