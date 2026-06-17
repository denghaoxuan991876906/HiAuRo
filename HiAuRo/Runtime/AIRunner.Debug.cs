using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    public DebugSnapshot Debug { get; } = new();

    public sealed class DebugSnapshot
    {
        public string Phase { get; set; } = "";
        public string SlotSource { get; set; } = "";
        public int HighPriGcd { get; set; }
        public int HighPriOgcd { get; set; }
        public int AbilityCount { get; set; }
        public int CurrGcdAbilityCount { get; set; }
        public float GcdRemain { get; set; }
        public bool CanGcd { get; set; }
        public bool CanOgcd { get; set; }
        public bool SlotState { get; set; }
        public bool HasNextSlot { get; set; }
        public bool HasWaitGcdSlot { get; set; }
        public bool HasCurrSlot { get; set; }
        public bool InSeq { get; set; }
        public string? CurrSeqName { get; set; }
        public string LastActionName { get; set; } = "";
        public List<ResolverInfo> Resolvers { get; } = new();

        public void Reset()
        {
            Phase = "";
            SlotSource = "";
            LastActionName = "";
            Resolvers.Clear();
        }
    }

    public sealed class ResolverInfo
    {
        public string Name { get; set; } = "";
        public string Mode { get; set; } = "";
        public int CheckResult { get; set; } = -99;
        public bool PassedWindow { get; set; }
    }
}
