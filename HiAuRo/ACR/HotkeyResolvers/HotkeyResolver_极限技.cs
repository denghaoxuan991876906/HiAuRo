using HiAuRo.Runtime;
using static HiAuRo.ACR.SpellsDefine;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键释放极限技
/// </summary>
public sealed class HotkeyResolver_极限技 : IHotkeyResolver
{
    public string Id => "lb";
    public string Label => "极限技";
    public string DefaultKey => "";
    public uint IconId => OmenTools.Interop.Game.Lumina.LuminaWrapper.GetActionIconID(极限技);

    public int Check()
    {
        return SpellHelper.CanUseSpell(极限技) ? 0 : -1;
    }

    public void Execute()
    {
        var slot = new Slot();
        slot.Add(new Spell
        {
            Id = 极限技,
            Name = "极限技",
            TargetType = SpellTargetType.Target,
            Type = SpellType.Ability,
            SpellCategory = SpellCategory.LimitBreak
        });
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }
}
