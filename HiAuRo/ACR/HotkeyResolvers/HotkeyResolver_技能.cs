using HiAuRo.Runtime;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键释放指定技能
/// </summary>
public sealed class HotkeyResolver_技能 : IHotkeyResolver
{
    private readonly uint _spellId;
    private readonly SpellTargetType _targetType;

    public string Id { get; }
    public string Label { get; }
    public string DefaultKey { get; }
    public uint IconId => LuminaWrapper.GetActionIconID(CurrentSpellId);

    /// <summary>当前实际释放的技能 ID，支持占星出卡等动态变化技能。</summary>
    private uint CurrentSpellId => _spellId.GetActionChange();

    /// <param name="label">显示名称</param>
    /// <param name="spellId">技能 ID</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="defaultKey">默认绑定键</param>
    public HotkeyResolver_技能(string label, uint spellId, SpellTargetType targetType = SpellTargetType.Target, string defaultKey = "")
    {
        Id = $"spell_{spellId}";
        Label = label;
        _spellId = spellId;
        _targetType = targetType;
        DefaultKey = defaultKey;
    }

    public void Execute()
    {
        var slot = new Slot();
        slot.Add(new Spell
        {
            Id = _spellId,
            Name = Label,
            TargetType = _targetType,
            Type = SpellType.Ability
        });
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }
}
