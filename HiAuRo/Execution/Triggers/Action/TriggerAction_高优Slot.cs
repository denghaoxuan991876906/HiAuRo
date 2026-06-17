using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("高优Slot", "高优先级技能槽执行")]
[TriggerTypeName("TriggerActionHighPrioritySlot")]
public sealed class TriggerAction_高优Slot : ITriggerAction
{
    public uint SpellId { get; set; }
    public SpellTargetType TargetType { get; set; } = SpellTargetType.Target;
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        ExecutionAxis.Instance.SetForceSpell(new Spell
        {
            Id = SpellId,
            Name = $"高优_{SpellId}",
            TargetType = TargetType,
            Type = SpellType.Ability
        });
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
        builder.AddDropdown("TargetType", Enum.GetNames<SpellTargetType>(), TargetType.ToString());
    }
}
