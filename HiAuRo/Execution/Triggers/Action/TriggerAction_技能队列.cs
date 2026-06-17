using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("技能队列", "将技能加入执行队列")]
[TriggerTypeName("TriggerActionSpellQueue")]
public sealed class TriggerAction_技能队列 : ITriggerAction
{
    public uint SpellId { get; set; }
    public SpellTargetType TargetType { get; set; } = SpellTargetType.Target;
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        var spell = new Spell
        {
            Id = SpellId,
            Name = $"队列_{SpellId}",
            TargetType = TargetType,
            Type = SpellType.Ability
        };

        var slot = new Slot();
        slot.Add(spell);
        ExecutionAxis.Instance.CurrentOutput.ForceSpell = spell;
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
        builder.AddDropdown("TargetType", Enum.GetNames<SpellTargetType>(), TargetType.ToString());
    }
}
