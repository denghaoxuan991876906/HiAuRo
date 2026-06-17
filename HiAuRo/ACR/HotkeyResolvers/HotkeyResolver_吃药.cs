using HiAuRo.Runtime;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键使用爆发药
/// </summary>
public sealed class HotkeyResolver_吃药 : IHotkeyResolver
{
    private readonly uint _itemId;
    public bool Ishq { get; }

    public string Id { get; }
    public string Label { get; }
    public string DefaultKey { get; }
    public uint IconId => LuminaWrapper.GetItemIconID(_itemId);

    /// <param name="label">显示名称</param>
    /// <param name="itemId">物品 ID（如刚力爆发药 39727）</param>
    /// <param name="ishq">是否 HQ</param>
    /// <param name="defaultKey">默认绑定键</param>
    public HotkeyResolver_吃药(string label, uint itemId, bool ishq = true, string defaultKey = "")
    {
        Id = $"potion_{itemId}";
        Label = label;
        _itemId = itemId;
        Ishq = ishq;
        DefaultKey = defaultKey;
    }

    public void Execute()
    {
        var slot = new Slot();
        slot.Add(Spell.CreatePotion());
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }
}
