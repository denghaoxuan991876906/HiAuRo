using HiAuRo.Runtime;
using OmenTools.Interop.Game.Lumina;
using static HiAuRo.ACR.SpellsDefine;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// QT 热键解析器 —— 极限技
/// </summary>
public sealed class HotkeyResolver_LB : IHotkeyResolver
{
    public string Id => "lb";
    public string Label => "极限技";
    public string DefaultKey => string.Empty;
    public uint IconId => LuminaWrapper.GetActionIconID(极限技);

    public int Check()
    {
        if (!SpellHelper.CanUseSpell(极限技)) return -1;
        return 0;
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
