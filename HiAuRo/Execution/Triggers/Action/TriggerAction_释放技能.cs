using HiAuRo.ACR;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("释放技能", "强制释放指定技能")]
[TriggerTypeName("TriggerActionCastSpell")]
public sealed class TriggerAction_释放技能 : ITriggerAction
{
    public uint SpellId { get; set; }
    public SpellTargetType TargetType { get; set; } = SpellTargetType.Target;
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        ExecutionAxis.Instance.SetForceSpell(new Spell
        {
            Id = SpellId,
            Name = $"执行轴_{SpellId}",
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
