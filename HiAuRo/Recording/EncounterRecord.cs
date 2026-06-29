using System.Diagnostics;
using System.Text.Json.Serialization;

namespace HiAuRo.Recording;

/// <summary>副本录制数据</summary>
public sealed class EncounterRecord
{
    /// <summary>数据版本</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>副本 ID</summary>
    [JsonPropertyName("territoryId")]
    public uint TerritoryId { get; set; }

    /// <summary>副本名称</summary>
    [JsonPropertyName("territoryName")]
    public string TerritoryName { get; set; } = "";

    /// <summary>录制时间</summary>
    [JsonPropertyName("recordedAt")]
    public string RecordedAt { get; set; } = "";

    /// <summary>总时长（毫秒）</summary>
    [JsonPropertyName("totalTimeMs")]
    public long TotalTimeMs { get; set; }

    /// <summary>队伍人数</summary>
    [JsonPropertyName("partySize")]
    public int PartySize { get; set; }

    /// <summary>职业组成</summary>
    [JsonPropertyName("jobComposition")]
    public List<string> JobComposition { get; set; } = [];

    /// <summary>Boss 列表</summary>
    [JsonPropertyName("bosses")]
    public List<BossInfo> Bosses { get; set; } = [];

    /// <summary>事件列表</summary>
    [JsonPropertyName("events")]
    public List<EncounterEvent> Events { get; set; } = [];
}

/// <summary>Boss 信息</summary>
public sealed class BossInfo
{
    /// <summary>Npc ID</summary>
    [JsonPropertyName("npcId")]
    public uint NpcId { get; set; }

    /// <summary>名称</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>最大 HP</summary>
    [JsonPropertyName("hpMax")]
    public uint HpMax { get; set; }
}

/// <summary>遭遇事件</summary>
public sealed class EncounterEvent
{
    /// <summary>事件时间（毫秒）</summary>
    [JsonPropertyName("timeMs")]
    public long TimeMs { get; set; }

    /// <summary>事件类型</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>事件分类</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    /// <summary>事件数据</summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object?> Data { get; set; } = [];
}

/// <summary>
/// 战斗时钟 — 进战归零，脱战暂停
/// </summary>
public sealed class CombatClock
{
    private readonly Stopwatch _sw = new();
    private long _baseMs;

    /// <summary>重置战斗时钟</summary>
    public void Reset()
    {
        _baseMs = 0;
        _sw.Restart();
    }

    /// <summary>暂停战斗时钟</summary>
    public void Pause()
    {
        _baseMs += _sw.ElapsedMilliseconds;
        _sw.Stop();
    }

    /// <summary>恢复战斗时钟</summary>
    public void Resume() => _sw.Start();

    /// <summary>当前战斗时间（毫秒）</summary>
    public long Now => _baseMs + _sw.ElapsedMilliseconds;
}
