using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("技能后", "检测指定技能使用后")]
[TriggerTypeName("TriggerCondAfterSpell")]
public sealed class TriggerCond_技能后 : ITriggerCond
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
