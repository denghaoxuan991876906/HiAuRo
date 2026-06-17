using System.Collections.Concurrent;
using System.Threading;
using HiAuRo.ACR;
using HiAuRo.ACR.Extension;
using HiAuRo.Execution.Events;

namespace HiAuRo.Infrastructure;

/// <summary>日志条目记录</summary>
/// <param name="Timestamp">时间戳</param>
/// <param name="Type">类型</param>
/// <param name="Content">内容</param>
public record LogEntry(DateTime Timestamp, string Type, string Content);

/// <summary>日志管理器 —— 内存环形缓冲 + 文件持久化</summary>
public sealed class LogManager : IDisposable
{
    /// <summary>日志管理器单例</summary>
    public static LogManager Instance { get; } = new();

    private const int MaxEntries = 5000;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly object _writeLock = new();
    private long _sequence;
    private uint _lastTerritoryId;
    private StreamWriter? _writer;

    /// <summary>初始化日志管理器</summary>
    public void Init(string configDir)
    {
        if (_writer != null)
        {
            try { _writer.Flush(); _writer.Dispose(); } catch { }
            _writer = null;
        }

        _lastTerritoryId = 0;
        _sequence = 0;

        var logDir = Path.Combine(configDir, "Logs");
        var logPath = Path.Combine(logDir, $"Logs-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        try
        {
            Directory.CreateDirectory(logDir);
            _writer = new StreamWriter(logPath, append: false)
            {
                AutoFlush = true
            };
            _writer.WriteLine($"# === Session Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            DService.Instance().Log.Information($"[LogManager] 日志文件: {logPath}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[LogManager] 无法创建日志文件: {ex.Message}");
        }
    }

    /// <summary>写入通用文本日志（供 Hi.Print / Hi.PrintError 等使用）</summary>
    public void WriteLine(string type, string content)
    {
        Interlocked.Increment(ref _sequence);
        var zoneCtx = GetZoneContext();
        var entry = new LogEntry(DateTime.Now, type, content);

        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);

        var writer = _writer;
        if (writer == null) return;

        try
        {
            lock (_writeLock)
            {
                var ts = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                writer.WriteLine(string.IsNullOrEmpty(zoneCtx)
                    ? $"{ts} | [{Hi.BattleTimeMs / 1000.0:F}] | {entry.Type} | {entry.Content}"
                    : $"{ts} | [{Hi.BattleTimeMs / 1000.0:F}] | {entry.Type} | {entry.Content} | {zoneCtx}");
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[LogManager] 写入失败: {ex.Message}");
        }
    }

    /// <summary>记录触发条件事件日志</summary>
    public void Log(ITriggerCondParams? p)
    {
        if (p == null) return;
        var type = GetLogType(p);
        if (type == null) return;
        if (p is AddStatusCondParams or EnemyCastSpellCondParams or ReceviceAbilityEffectCondParams)
        {
            var sourceId = p switch
            {
                AddStatusCondParams b => b.SourceID,
                EnemyCastSpellCondParams c => c.SourceID,
                ReceviceAbilityEffectCondParams e => e.SourceID,
                _ => 0u
            };
            var obj = DService.Instance().ObjectTable.SearchByEntityID(sourceId);
            if (obj is not null)
            {
                if (!(obj.ObjectKind is ObjectKind.BattleNpc or ObjectKind.EventNpc))
                    return;
            }
        }
        WriteLine(type, p.ToString() ?? "");
    }

    /// <summary>
    /// 每种事件的日志类型名。<c>null</c> = 不记录。
    /// 要过滤/恢复某种事件，改这里即可。
    /// </summary>
    private static string? GetLogType(ITriggerCondParams p) => p switch
    {
        ActorControlParams => null,       // 过滤
        RemoveStatusCondParams => null,       // 过滤
        AddStatusCondParams => "AddBuff",
        EnemyCastSpellCondParams => "CastSpell",
        ReceviceAbilityEffectCondParams => "SkillEffect",
        ReceviceNoTargetAbilityEffectCondParams => "SkillNoTarget",
        TetherCreateParams => "TetherAdd",
        TetherSnapshotParams => "TetherSnapshot",
        TetherRemoveParams => "TetherRemove",
        ActorDeathParams => "Death",
        TargetIconEffectTestCondParams => "Icon",
        TargetAbleCondParams => "TargetAble",
        ActorControlCombatParams => "CombatState",
        PlayActionTimelineParams => "Timeline",
        NpcYellCondParams => "NpcYell",
        OnMapEffectCreateEvent => "MapEffect",
        OnEnvControlEvent => "EnvControl",
        UnitCreateCondParams => "UnitCreate",
        UnitDeleteCondParams => "UnitDelete",
        WeatherChangedCondParams => "Weather",
        AfterSpellCondParams => "AfterSpell",
        CombatStateParams => "CombatState",
        BattleStartCondParams => "BattleStart",
        BattleEndCondParams => "BattleEnd",
        GameLogCondParams => "GameLog",
        DirectorUpdateParams => "DirectorUpdate",
        ObjectEffectParams => "ObjectEffect",
        ObjectChangeParams => "ObjectChange",
        ObjectSpawnParams => "ObjectSpawn",
        _ => p.GetType().Name
    };

    /// <summary>获取所有日志条目</summary>
    public IReadOnlyList<LogEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    /// <summary>清空日志</summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        _sequence = 0;
    }

    /// <summary>释放日志文件资源</summary>
    public void Dispose()
    {
        try
        {
            _writer?.WriteLine($"# === Session End: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _writer?.Flush();
            _writer?.Dispose();
        }
        catch { }
        _writer = null;
    }

    /// <summary>获取当前副本上下文，进入新区时返回 Territory 信息</summary>
    private string GetZoneContext()
    {
        try
        {
            var terri = DService.Instance().ClientState?.TerritoryType ?? 0;
            if (terri == _lastTerritoryId || terri == 0)
                return "";

            _lastTerritoryId = terri;
            try
            {
                var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
                var row = sheet?.GetRow(terri);
                if (row.HasValue && !string.IsNullOrEmpty(row.Value.PlaceName.Value.Name.ToString()))
                    return $"Territory={terri} (0x{terri:X}) \"{row.Value.PlaceName.Value.Name}\"";
            }
            catch { }
            return $"Territory={terri} (0x{terri:X})";
        }
        catch
        {
            return "";
        }
    }
}
