using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("上次技能", "检测上次使用的技能是否为指定技能")]
[TriggerTypeName("TriggerCondCheckLastSpell")]
public sealed class TriggerCond_上次技能 : ITriggerCond
{
    public uint SpellId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return EventSystem.LastCompletedActionId == SpellId;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
    }
}
