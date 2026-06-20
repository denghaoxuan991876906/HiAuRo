namespace HiAuRo.Recording;

/// <summary>用于 1 秒窗口差异计算的轻量帧快照。</summary>
public sealed class CombatFrameCacheSample
{
    public long SampleIndex { get; set; }
    public long Tick { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string SessionId { get; set; } = "";
    public uint Job { get; set; }
    public uint Hp { get; set; }
    public uint MaxHp { get; set; }
    public uint Mp { get; set; }
    public uint MaxMp { get; set; }
    public ushort Level { get; set; }
    public bool InCombat { get; set; }
    public bool IsMoving { get; set; }
    public bool IsCasting { get; set; }
    public float TotalCastTime { get; set; }
    public float CurrentCastTime { get; set; }
    public float GcdCooldown { get; set; }
    public float GcdDuration { get; set; }
    public uint LastComboSpellId { get; set; }
    public uint TargetId { get; set; }
    public string TargetName { get; set; } = "";
    public float Distance { get; set; }
    public bool IsBoss { get; set; }
    public bool IsDead { get; set; }
    public bool IsTargetable { get; set; }
    public uint TargetHp { get; set; }
    public uint TargetMaxHp { get; set; }
    public Dictionary<string, bool> QtStates { get; set; } = [];
    public Dictionary<string, object?> JobGauge { get; set; } = [];
    public List<CombatBuffSnapshot> SelfBuffs { get; set; } = [];
    public List<CombatBuffSnapshot> TargetBuffs { get; set; } = [];
}
