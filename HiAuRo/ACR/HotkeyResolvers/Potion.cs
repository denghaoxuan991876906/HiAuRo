using HiAuRo.Runtime;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// QT 热键解析器 —— 爆发药
/// </summary>
public sealed class HotkeyResolver_Potion : IHotkeyResolver
{
    public string Id => "potion";
    public string Label => "爆发药";
    public string DefaultKey => string.Empty;
    public uint IconId => LuminaWrapper.GetItemIconID(44163);

    public int Check()
    {
        return ItemHelper.CheckCurrJobPotion() ? 0 : -1;
    }

    public void Execute()
    {
        var slot = new Slot();
        slot.Add(Spell.CreatePotion());
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }
}
