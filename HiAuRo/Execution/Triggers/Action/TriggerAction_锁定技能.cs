using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("锁定技能", "锁定/解锁指定技能")]
[TriggerTypeName("TriggerActionLockSpell")]
public sealed class TriggerAction_锁定技能 : ITriggerAction
{
    public uint SpellId { get; set; }
    public bool Locked { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        if (Locked)
        {
            ExecutionAxis.Instance.SetForceSpell(null!);
        }
        else
        {
            ExecutionAxis.Instance.CurrentOutput.ForceSpell = null;
        }
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
        builder.AddCheckbox("Locked", Locked);
    }
}
