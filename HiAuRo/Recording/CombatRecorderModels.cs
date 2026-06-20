using System.Text.Json.Serialization;

namespace HiAuRo.Recording;

/// <summary>Combat Recorder 采样来源。</summary>
public enum CombatRecorderSourceMode
{
    Manual = 0,
    Acr = 1,
    Unknown = 2
}

/// <summary>触发一次事件采样的原因。</summary>
public enum CombatRecorderEventType
{
    Unknown,
    QueuedActionChanged,
    CastStart,
    UseActionSuccess,
    ActionEffect,
    AbilityEffect,
    GcdReadyAndAction,
    CastCancelled,
    ActionRejected,
    StallDetected
}

public enum CombatRecorderGcdKind
{
    Unknown,
    Instant,
    Casted
}

/// <summary>单条 jsonl 技能采样快照。</summary>
public sealed class CombatFrameSnapshot
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("frameIndex")]
    public long FrameIndex { get; set; }

    [JsonPropertyName("sampleIndex")]
    public long SampleIndex { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("eventGroupId")]
    public string EventGroupId { get; set; } = "";

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = "";

    [JsonPropertyName("sampleRole")]
    public string SampleRole { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "unknown";

    [JsonPropertyName("job")]
    public uint Job { get; set; }

    [JsonPropertyName("actionId")]
    public uint ActionId { get; set; }

    [JsonPropertyName("actionName")]
    public string ActionName { get; set; } = "";

    [JsonPropertyName("queuedActionId")]
    public uint QueuedActionId { get; set; }

    [JsonPropertyName("castActionId")]
    public uint CastActionId { get; set; }

    [JsonPropertyName("isGcd")]
    public bool IsGcd { get; set; }

    [JsonPropertyName("isAbility")]
    public bool IsAbility { get; set; }

    [JsonPropertyName("gcdKind")]
    public CombatRecorderGcdKind GcdKind { get; set; }

    [JsonPropertyName("success")]
    public bool? Success { get; set; }

    [JsonPropertyName("failed")]
    public bool? Failed { get; set; }

    [JsonPropertyName("cancelled")]
    public bool? Cancelled { get; set; }

    [JsonPropertyName("hp")]
    public uint Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public uint MaxHp { get; set; }

    [JsonPropertyName("hpPercent")]
    public float HpPercent { get; set; }

    [JsonPropertyName("mp")]
    public uint Mp { get; set; }

    [JsonPropertyName("maxMp")]
    public uint MaxMp { get; set; }

    [JsonPropertyName("mpPercent")]
    public float MpPercent { get; set; }

    [JsonPropertyName("level")]
    public ushort Level { get; set; }

    [JsonPropertyName("inCombat")]
    public bool InCombat { get; set; }

    [JsonPropertyName("isMoving")]
    public bool IsMoving { get; set; }

    [JsonPropertyName("isCasting")]
    public bool IsCasting { get; set; }

    [JsonPropertyName("totalCastTime")]
    public float TotalCastTime { get; set; }

    [JsonPropertyName("currentCastTime")]
    public float CurrentCastTime { get; set; }

    [JsonPropertyName("gcdCooldown")]
    public float GcdCooldown { get; set; }

    [JsonPropertyName("gcdDuration")]
    public float GcdDuration { get; set; }

    [JsonPropertyName("lastComboSpellId")]
    public uint LastComboSpellId { get; set; }

    [JsonPropertyName("selfBuffs")]
    public List<CombatBuffSnapshot> SelfBuffs { get; set; } = [];

    [JsonPropertyName("targetId")]
    public uint TargetId { get; set; }

    [JsonPropertyName("targetName")]
    public string TargetName { get; set; } = "";

    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("isBoss")]
    public bool IsBoss { get; set; }

    [JsonPropertyName("isDead")]
    public bool IsDead { get; set; }

    [JsonPropertyName("isTargetable")]
    public bool IsTargetable { get; set; }

    [JsonPropertyName("targetHp")]
    public uint TargetHp { get; set; }

    [JsonPropertyName("targetMaxHp")]
    public uint TargetMaxHp { get; set; }

    [JsonPropertyName("targetHpPercent")]
    public float TargetHpPercent { get; set; }

    [JsonPropertyName("targetBuffs")]
    public List<CombatBuffSnapshot> TargetBuffs { get; set; } = [];

    [JsonPropertyName("enemyCountNearSelf_5")]
    public int EnemyCountNearSelf5 { get; set; }

    [JsonPropertyName("enemyCountNearSelf_10")]
    public int EnemyCountNearSelf10 { get; set; }

    [JsonPropertyName("enemyCountNearSelf_15")]
    public int EnemyCountNearSelf15 { get; set; }

    [JsonPropertyName("enemyCountNearSelf_25")]
    public int EnemyCountNearSelf25 { get; set; }

    [JsonPropertyName("enemyCountNearTarget_5")]
    public int EnemyCountNearTarget5 { get; set; }

    [JsonPropertyName("enemyCountNearTarget_10")]
    public int EnemyCountNearTarget10 { get; set; }

    [JsonPropertyName("enemyCountNearTarget_15")]
    public int EnemyCountNearTarget15 { get; set; }

    [JsonPropertyName("enemyCountNearTarget_25")]
    public int EnemyCountNearTarget25 { get; set; }

    [JsonPropertyName("jobGauge")]
    public Dictionary<string, object?> JobGauge { get; set; } = [];

    [JsonPropertyName("qtStates")]
    public Dictionary<string, bool> QtStates { get; set; } = [];

    [JsonPropertyName("currentAcrName")]
    public string CurrentAcrName { get; set; } = "";

    [JsonPropertyName("currentRotationName")]
    public string CurrentRotationName { get; set; } = "";

    [JsonPropertyName("resolverResults")]
    public List<CombatResolverSnapshot> ResolverResults { get; set; } = [];

    [JsonPropertyName("trackedSkills")]
    public List<CombatTrackedSkillSnapshot> TrackedSkills { get; set; } = [];

    [JsonPropertyName("before")]
    public CombatFrameSnapshot? Before { get; set; }

    [JsonPropertyName("runnerDebug")]
    public CombatRunnerDebugSnapshot? RunnerDebug { get; set; }
}

public sealed class CombatBuffSnapshot
{
    [JsonPropertyName("id")]
    public uint Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("stack")]
    public ushort Stack { get; set; }

    [JsonPropertyName("remainMs")]
    public int RemainMs { get; set; }
}

public sealed class CombatResolverSnapshot
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("checkResult")]
    public int CheckResult { get; set; }

    [JsonPropertyName("passedWindow")]
    public bool PassedWindow { get; set; }
}

public sealed class CombatTrackedSkillSnapshot
{
    [JsonPropertyName("actionId")]
    public uint ActionId { get; set; }

    [JsonPropertyName("actionName")]
    public string ActionName { get; set; } = "";

    [JsonPropertyName("cooldownRemainingMs")]
    public float CooldownRemainingMs { get; set; }

    [JsonPropertyName("charges")]
    public int Charges { get; set; }

    [JsonPropertyName("maxCharges")]
    public int MaxCharges { get; set; }
}

public sealed class CombatRunnerDebugSnapshot
{
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "";

    [JsonPropertyName("slotSource")]
    public string SlotSource { get; set; } = "";

    [JsonPropertyName("canGcd")]
    public bool CanGcd { get; set; }

    [JsonPropertyName("canOgcd")]
    public bool CanOgcd { get; set; }

    [JsonPropertyName("hasNextSlot")]
    public bool HasNextSlot { get; set; }

    [JsonPropertyName("hasWaitGcdSlot")]
    public bool HasWaitGcdSlot { get; set; }

    [JsonPropertyName("hasCurrSlot")]
    public bool HasCurrSlot { get; set; }
}
