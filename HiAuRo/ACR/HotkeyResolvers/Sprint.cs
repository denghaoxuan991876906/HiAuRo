using FFXIVClientStructs.FFXIV.Client.Game;
using HiAuRo.Runtime;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;
using static HiAuRo.ACR.SpellsDefine;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// QT 热键解析器 —— 疾跑
/// </summary>
public sealed class HotkeyResolver_Sprint : IHotkeyResolver
{
    public string Id => "sprint";
    public string Label => "疾跑";
    public string DefaultKey => string.Empty;
    public uint IconId => LuminaWrapper.GetActionIconID(疾跑);

    public int Check()
    {
        if (!UseActionManager.Instance().IsActionOffCooldown(ActionType.GeneralAction, 疾跑)) return -1;
        return 0;
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
        SlotHelper.Execute(slot);
    }
}
