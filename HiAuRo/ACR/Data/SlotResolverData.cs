namespace HiAuRo.ACR;

/// <summary>
/// 技能槽位数据 = ISlotResolver + SlotMode
/// </summary>
public sealed class SlotResolverData
{
    public required ISlotResolver Resolver { get; init; }
    public required SlotMode Mode { get; init; }
}
