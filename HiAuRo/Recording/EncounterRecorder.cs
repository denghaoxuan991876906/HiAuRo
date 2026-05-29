using System.Text.Json;
using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using HiAuRo.Runtime;
using OmenTools.OmenService;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.Recording;

/// <summary>副本录制器 — 录制战斗事件用于调试/可视化回放</summary>
public sealed class EncounterRecorder
{
    /// <summary>录制器单例</summary>
    public static EncounterRecorder Instance { get; } = new();

    private readonly CombatClock _clock = new();
    private readonly Dictionary<Type, Func<ITriggerCondParams, long, EncounterEvent>> _serializers = [];
    private readonly HashSet<uint> _bossNpcIds = [];
    private readonly object _lock = new();
    private EncounterRecord? _current;
    private bool _initialized;
    private string _saveDir = "";

    private EncounterRecorder() { }

    #region 生命周期

    /// <summary>初始化录制器</summary>
    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _saveDir = Path.Combine(
            DService.Instance().PI.ConfigDirectory.FullName, "Recordings");
        Directory.CreateDirectory(_saveDir);

        GameEventHook.Instance.OnEventFired += OnGameEvent;
        CombatContext.StateChanged += OnCombatStateChanged;
        RegisterSerializers();

        _bossNpcIds.Clear();
    }

    /// <summary>关闭录制器</summary>
    public void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;

        GameEventHook.Instance.OnEventFired -= OnGameEvent;
        CombatContext.StateChanged -= OnCombatStateChanged;

        bool hasPending;
        lock (_lock)
        {
            hasPending = _current != null && _current.Events.Count > 0;
        }
        if (hasPending)
            SaveRecord();

        _serializers.Clear();
        lock (_lock) { _bossNpcIds.Clear(); }
    }

    #endregion

    #region 战斗状态回调

    private void OnCombatStateChanged(CombatContext.State oldState, CombatContext.State newState)
    {
        if (newState == CombatContext.State.InCombat)
        {
            if (!PluginConfig.Instance.RecordingEnabled) return;
            StartRecording();
        }
        else if (newState == CombatContext.State.OutOfCombat)
        {
            bool shouldSave;
            lock (_lock) { shouldSave = _current != null; }
            if (shouldSave)
            {
                _clock.Pause();
                SaveRecord();
            }
        }
    }

    private void StartRecording()
    {
        _clock.Reset();

        var rec = new EncounterRecord
        {
            TerritoryId = GameState.TerritoryType,
            TerritoryName = GetTerritoryName(),
            RecordedAt = DateTime.UtcNow.ToString("O"),
            Events = []
        };

        lock (_lock) { _current = rec; }

        // 采集队伍信息
        try
        {
            var pt = DService.Instance().PartyList;
            if (pt != null)
            {
                rec.PartySize = pt.Length;
                var jobs = new List<string>();
                foreach (var member in pt)
                {
                    if (member?.ClassJob != null)
                    {
                        var name = member.ClassJob.ToString();
                        if (!string.IsNullOrEmpty(name))
                            jobs.Add(name);
                    }
                }
                rec.JobComposition = jobs;
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Debug($"[Recording] 队伍信息采集异常: {ex.Message}");
        }
    }

    private string GetTerritoryName()
    {
        try
        {
            var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
            if (sheet != null)
            {
                var row = sheet.GetRow(GameState.TerritoryType);
                var placeName = row.PlaceName.Value;
                return placeName.Name.ToString();
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Debug($"[Recording] Territory名称获取异常: {ex.Message}");
        }
        return $"Territory_{GameState.TerritoryType}";
    }

    private void SaveRecord()
    {
        if (!_initialized) return;

        EncounterRecord? record;
        uint[] bossIds;

        lock (_lock)
        {
            if (_current == null) return;

            _current.TotalTimeMs = _clock.Now;
            bossIds = _bossNpcIds.ToArray();
            record = _current;
            _current = null;
            _bossNpcIds.Clear();
        }

        record.Bosses = bossIds.Select(id =>
        {
            var obj = DService.Instance().ObjectTable
                .FirstOrDefault(o => o != null && o.EntityID == id);
            var bn = obj as IBattleNPC;
            return new BossInfo
            {
                NpcId = bn?.DataID ?? 0,
                Name = obj?.Name.ToString() ?? "",
                HpMax = bn?.MaxHp ?? 0
            };
        }).ToList();

        var json = JsonSerializer.Serialize(record,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var safeName = SanitizeFileName(record.TerritoryName) + "_" +
                       DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
        var path = Path.Combine(_saveDir, safeName);

        File.WriteAllText(path, json);
        DService.Instance().Log.Information($"[Recording] 已保存: {path} ({record.Events.Count} 事件)");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var arr = name.Where(c => !invalid.Contains(c)).ToArray();
        return new string(arr);
    }

    #endregion

    #region Public properties (for ImGui panel)

    /// <summary>是否正在录制</summary>
    public bool IsRecording
    {
        get { lock (_lock) { return _current != null; } }
    }

    /// <summary>已录制秒数</summary>
    public int ElapsedSeconds => (int)(_clock.Now / 1000);

    /// <summary>当前录制文件名</summary>
    public string CurrentFileName
    {
        get
        {
            lock (_lock)
            {
                if (_current == null) return "";
                var safe = SanitizeFileName(_current.TerritoryName) + "_" +
                           DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
                return safe;
            }
        }
    }

    /// <summary>获取录制文件列表</summary>
    public (string Name, string Path)[] GetRecordFiles()
    {
        if (!Directory.Exists(_saveDir)) return [];
        return Directory.GetFiles(_saveDir, "*.json")
            .Select(f => (Path.GetFileName(f), f))
            .OrderByDescending(x => x.Item2)
            .ToArray();
    }

    /// <summary>强制停止录制并保存</summary>
    public void ForceStop()
    {
        bool shouldSave;
        lock (_lock) { shouldSave = _current != null; }
        if (shouldSave)
        {
            _clock.Pause();
            SaveRecord();
        }
    }

    #endregion

    #region 事件序列化

    private void RegisterSerializers()
    {
        // ---- cast ----
        _serializers[typeof(ActorCastParams)] = (p, t) =>
        {
            var cp = (ActorCastParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorCastParams),
                Category = "cast",
                Data = new()
                {
                    ["actionId"] = cp.ActionID,
                    ["castTime"] = cp.CastTime,
                    ["targetId"] = cp.TargetID,
                    ["sourceId"] = cp.SourceID,
                    ["posX"] = cp.PosX,
                    ["posY"] = cp.PosY,
                    ["posZ"] = cp.PosZ,
                }
            };
        };

        _serializers[typeof(AfterSpellParams)] = (p, t) =>
        {
            var ap = (AfterSpellParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(AfterSpellParams),
                Category = "cast",
                Data = new() { ["spellId"] = ap.SpellID }
            };
        };

        // ---- ability ----
        _serializers[typeof(ActionEffectParams)] = (p, t) =>
        {
            var ap = (ActionEffectParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActionEffectParams),
                Category = "ability",
                Data = new()
                {
                    ["actionId"] = ap.ActionID,
                    ["sourceId"] = ap.SourceID,
                    ["targetOid"] = ap.TargetOID,
                    ["animationId"] = ap.AnimationID,
                    ["effectType"] = ap.EffectType,
                }
            };
        };

        _serializers[typeof(NoTargetAbilityEffectParams)] = (p, t) =>
        {
            var np = (NoTargetAbilityEffectParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(NoTargetAbilityEffectParams),
                Category = "ability",
                Data = new()
                {
                    ["sourceId"] = np.SourceID,
                    ["actionId"] = np.ActionID,
                    ["posX"] = np.PosX,
                    ["posY"] = np.PosY,
                    ["posZ"] = np.PosZ,
                }
            };
        };

        // ---- buff ----
        _serializers[typeof(BuffGainParams)] = (p, t) =>
        {
            var bp = (BuffGainParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(BuffGainParams),
                Category = "buff",
                Data = new()
                {
                    ["sourceId"] = bp.SourceID,
                    ["statusId"] = bp.StatusID,
                    ["stackCount"] = bp.StackCount,
                }
            };
        };

        _serializers[typeof(BuffRemoveParams)] = (p, t) =>
        {
            var bp = (BuffRemoveParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(BuffRemoveParams),
                Category = "buff",
                Data = new()
                {
                    ["sourceId"] = bp.SourceID,
                    ["statusId"] = bp.StatusID,
                }
            };
        };

        // ---- tether ----
        _serializers[typeof(TetherCreateParams)] = (p, t) =>
        {
            var tp = (TetherCreateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(TetherCreateParams),
                Category = "tether",
                Data = new()
                {
                    ["tetherId"] = tp.TetherID,
                    ["sourceId"] = tp.SourceID,
                    ["targetOid"] = tp.TargetOID,
                    ["param2"] = tp.Param2,
                    ["param3"] = tp.Param3,
                    ["param5"] = tp.Param5,
                }
            };
        };

        _serializers[typeof(TetherRemoveParams)] = (p, t) =>
        {
            var tp = (TetherRemoveParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(TetherRemoveParams),
                Category = "tether",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["param2"] = tp.Param2,
                    ["param3"] = tp.Param3,
                    ["param5"] = tp.Param5,
                }
            };
        };

        // ---- spawn ----
        _serializers[typeof(UnitCreateParams)] = (p, t) =>
        {
            var up = (UnitCreateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(UnitCreateParams),
                Category = "spawn",
                Data = new()
                {
                    ["entityId"] = up.EntityId,
                    ["dataId"] = up.DataId,
                    ["name"] = up.Name,
                }
            };
        };

        _serializers[typeof(UnitDeleteParams)] = (p, t) =>
        {
            var up = (UnitDeleteParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(UnitDeleteParams),
                Category = "spawn",
                Data = new()
                {
                    ["entityId"] = up.EntityId,
                    ["dataId"] = up.DataId,
                    ["name"] = up.Name,
                }
            };
        };

        // ---- death ----
        _serializers[typeof(ActorControlDeathParams)] = (p, t) =>
        {
            var dp = (ActorControlDeathParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlDeathParams),
                Category = "death",
                Data = new()
                {
                    ["sourceId"] = dp.SourceID,
                    ["targetId"] = dp.TargetID,
                }
            };
        };

        // ---- target ----
        _serializers[typeof(ActorControlTargetIconParams)] = (p, t) =>
        {
            var tp = (ActorControlTargetIconParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlTargetIconParams),
                Category = "target",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["targetId"] = tp.TargetID,
                    ["iconId"] = tp.IconID,
                }
            };
        };

        _serializers[typeof(ActorControlTargetableParams)] = (p, t) =>
        {
            var tp = (ActorControlTargetableParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlTargetableParams),
                Category = "target",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["targetId"] = tp.TargetID,
                    ["isTargetable"] = tp.IsTargetable,
                }
            };
        };

        // ---- combat ----
        _serializers[typeof(CombatStateParams)] = (p, t) =>
        {
            var cp = (CombatStateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(CombatStateParams),
                Category = "combat",
                Data = new() { ["isEntering"] = cp.IsEntering }
            };
        };

        _serializers[typeof(ActorControlCombatParams)] = (p, t) =>
        {
            var cp = (ActorControlCombatParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlCombatParams),
                Category = "combat",
                Data = new() { ["isEntering"] = cp.IsEntering }
            };
        };

        // ---- environment ----
        _serializers[typeof(MapEffectParams)] = (p, t) =>
        {
            var mp = (MapEffectParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(MapEffectParams),
                Category = "environment",
                Data = new()
                {
                    ["positionIndex"] = mp.PositionIndex,
                    ["param1"] = mp.Param1,
                    ["param2"] = mp.Param2,
                }
            };
        };

        _serializers[typeof(EnvControlParams)] = (p, t) =>
        {
            var ep = (EnvControlParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(EnvControlParams),
                Category = "environment",
                Data = new()
                {
                    ["index"] = ep.Index,
                    ["flag"] = ep.Flag,
                }
            };
        };

        _serializers[typeof(WeatherChangedParams)] = (p, t) =>
        {
            var wp = (WeatherChangedParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(WeatherChangedParams),
                Category = "environment",
                Data = new() { ["newWeatherId"] = wp.NewWeatherId }
            };
        };

        // ---- npc ----
        _serializers[typeof(NpcYellParams)] = (p, t) =>
        {
            var np = (NpcYellParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(NpcYellParams),
                Category = "npc",
                Data = new()
                {
                    ["sourceId"] = np.SourceID,
                    ["sourceName"] = np.SourceName,
                    ["yellId"] = np.YellID,
                    ["yellMsg"] = np.YellMsg,
                }
            };
        };

        // ---- director ----
        _serializers[typeof(DirectorUpdateParams)] = (p, t) =>
        {
            var dp = (DirectorUpdateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(DirectorUpdateParams),
                Category = "director",
                Data = new()
                {
                    ["category"] = dp.Category.ToString(),
                    ["param1"] = dp.Param1,
                    ["param2"] = dp.Param2,
                    ["param3"] = dp.Param3,
                    ["param4"] = dp.Param4,
                    ["a6"] = dp.A6,
                    ["a7"] = dp.A7,
                    ["a8"] = dp.A8,
                    ["a9"] = dp.A9,
                }
            };
        };

        _serializers[typeof(ActorControlTimelineParams)] = (p, t) =>
        {
            var tp = (ActorControlTimelineParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlTimelineParams),
                Category = "director",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["timelineId"] = tp.TimelineID,
                }
            };
        };

        // ---- actorControl ----
        _serializers[typeof(ActorControlParams)] = (p, t) =>
        {
            var ap = (ActorControlParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlParams),
                Category = "actorControl",
                Data = new()
                {
                    ["sourceId"] = ap.SourceID,
                    ["command"] = ap.Command,
                    ["p1"] = ap.P1,
                    ["p2"] = ap.P2,
                    ["p3"] = ap.P3,
                    ["p4"] = ap.P4,
                    ["p5"] = ap.P5,
                    ["p6"] = ap.P6,
                    ["targetId"] = ap.TargetID,
                }
            };
        };

        // ---- chat ----
        _serializers[typeof(ChatMessageParams)] = (p, t) =>
        {
            var cp = (ChatMessageParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ChatMessageParams),
                Category = "chat",
                Data = new() { ["message"] = cp.Message }
            };
        };

        _serializers[typeof(ObjectChangeParams)] = (p, t) =>
        {
            var op = (ObjectChangeParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ObjectChangeParams),
                Category = "actorControl",
                Data = new()
                {
                    ["sourceId"] = op.SourceID,
                    ["targetId"] = op.TargetID,
                    ["command"]  = op.Command,
                    ["p1"] = op.P1, ["p2"] = op.P2, ["p3"] = op.P3, ["p4"] = op.P4
                }
            };
        };

        _serializers[typeof(ObjectEffectParams)] = (p, t) =>
        {
            var op = (ObjectEffectParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ObjectEffectParams),
                Category = "environment",
                Data = new()
                {
                    ["objectId"] = op.ObjectID,
                    ["data1"]    = op.Data1,
                    ["data2"]    = op.Data2
                }
            };
        };
    }

    private void OnGameEvent(ITriggerCondParams condParams)
    {
        lock (_lock)
        {
            if (_current == null) return;

            // 尝试识别 Boss NPC (来自 cast 事件的高HP敌人)
            if (condParams is ActorCastParams acp && acp.SourceID != 0)
            {
                try
                {
                    var obj = DService.Instance().ObjectTable
                        .FirstOrDefault(o => o != null && o.EntityID == acp.SourceID);
                    if (obj is IBattleNPC bn && bn.MaxHp > 1000)
                    {
                        _bossNpcIds.Add(acp.SourceID);
                    }
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Debug($"[Recording] Boss检测异常: {ex.Message}");
                }
            }

            var type = condParams.GetType();
            if (!_serializers.TryGetValue(type, out var serializer)) return;

            try
            {
                var evt = serializer(condParams, _clock.Now);
                _current.Events.Add(evt);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[Recording] 序列化失败 {type.Name}: {ex}");
            }
        }
    }

    #endregion
}
