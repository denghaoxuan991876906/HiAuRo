using HiAuRo.Runtime;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// QT 热键解析器 —— 释放指定技能
/// </summary>
public sealed class HotkeyResolver_NormalSpell : IHotkeyResolver
{
    private readonly uint _spellId;
    private readonly SpellTargetType _targetType;
    private readonly string _name;
    private readonly uint _iconId;

    public string Id { get; }
    public string Label { get; }
    public string DefaultKey => string.Empty;
    public uint IconId => _iconId;

    public HotkeyResolver_NormalSpell(uint spellId, string name, SpellTargetType targetType = SpellTargetType.Target)
    {
        _spellId = spellId;
        _name = name;
        _targetType = targetType;
        _iconId = LuminaWrapper.GetActionIconID(spellId);
        Id = $"spell_{spellId}";
        Label = name;
    }

    public int Check()
    {
        if (!SpellHelper.CanUseSpell(_spellId)) return -1;
        return 0;
    }

    public void Execute()
    {
        var slot = new Slot();
        slot.Add(new Spell
        {
            Id = _spellId,
            Name = _name,
            TargetType = _targetType,
            Type = SpellType.Ability
        });
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }
}
