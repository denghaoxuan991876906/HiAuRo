using FFXIVClientStructs.FFXIV.Client.Game;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键释放指定技能
/// </summary>
public sealed class HotkeyResolver_技能 : IHotkeyResolver
{
    private readonly uint _spellId;

    public string Id { get; }
    public string Label { get; }
    public string DefaultKey { get; }
    public uint IconId => LuminaWrapper.GetActionIconID(CurrentSpellId);

    /// <summary>当前实际释放的技能 ID，支持占星出卡等动态变化技能。</summary>
    private uint CurrentSpellId => _spellId.GetActionChange();

    /// <param name="id">热键 ID（如 "Pot_爆发药"）</param>
    /// <param name="label">显示名称</param>
    /// <param name="spellId">技能 ID</param>
    /// <param name="defaultKey">默认绑定键</param>
    public HotkeyResolver_技能(string label, uint spellId, string defaultKey = "")
    {
        Id = label + Guid.NewGuid().ToString("N")[0..8];
        Label = label;
        _spellId = spellId;
        DefaultKey = defaultKey;
    }

    public void Execute()
    {
        OmenTools.OmenService.UseActionManager.Instance().UseAction(
            ActionType.Action, CurrentSpellId, 0, 0, 0, 0);
    }
}
