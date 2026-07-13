namespace HiAuRo.ACR;

public interface IBasicAcrScript
{
    Jobs TargetJob { get; }

    IReadOnlyList<SlotResolverData> BuildSlotResolvers();
}
