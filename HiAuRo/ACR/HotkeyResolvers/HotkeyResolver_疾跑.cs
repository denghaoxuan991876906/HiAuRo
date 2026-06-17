using HiAuRo.Runtime;
using static HiAuRo.ACR.SpellsDefine;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键使用疾跑
/// </summary>
public sealed class HotkeyResolver_疾跑 : IHotkeyResolver
{
    public string Id => "sprint";
    public string Label => "疾跑";
    public string DefaultKey => "";
    public uint IconId => OmenTools.Interop.Game.Lumina.LuminaWrapper.GetActionIconID(疾跑);

    public int Check()
    {
        return SpellHelper.CanUseSpell(疾跑) ? 0 : -1;
    }

    public void Execute()
    {
        var slot = new Slot();
        slot.Add(new Spell
        {
            Id = 疾跑,
            Name = "疾跑",
            TargetType = SpellTargetType.Self,
            Type = SpellType.Ability,
            SpellCategory = SpellCategory.Sprint
        });
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }
}
