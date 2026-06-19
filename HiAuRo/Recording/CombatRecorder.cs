using System.Diagnostics;
using System.Text.Json;
using Dalamud.Game.ClientState.JobGauge.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using HiAuRo.Runtime;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;
using ActionTypeFF = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace HiAuRo.Recording;

/// <summary>战斗操作录制器：只在技能相关事件点写 jsonl，平时只维护最近帧缓存。</summary>
public sealed class CombatRecorder
{
    public static CombatRecorder Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _lock = new();
    private readonly CombatFrameRingBuffer _frames = new(5);
    private string _sessionId = "";
    private string _sessionPath = "";
    private long _sampleIndex;
    private long _frameIndex;
    private long _lastCacheAt;
    private bool _initialized;
    private bool _lastManualPauseApplied;
    private uint _lastEffectActionId;
    private long _lastEffectAt;
    private uint _pendingCastGcdActionId;
    private long _pendingCastGcdRecordedAt;

    private CombatRecorder() { }

    public bool IsRecording => IsEnabledForCurrentState();

    public string SessionId => _sessionId;

    public string CurrentLogPath => _sessionPath;

    public string LogRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HiAuRo",
        "RecorderLogs");

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        EventSystem.RegisterOnUseAction(OnUseAction);
        GameEventHook.Instance.OnEventFired += OnGameEvent;
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;

        EventSystem.UnregisterOnUseAction(OnUseAction);
        GameEventHook.Instance.OnEventFired -= OnGameEvent;
        _frames.Clear();
    }

    public void UpdateFrameCache()
    {
        if (!_initialized) return;

        ApplyManualSourcePause();

        if (!IsEnabledForCurrentState())
        {
            _frames.Clear();
            ResetTransientState();
            return;
        }

        var interval = Math.Clamp(PluginConfig.Instance.CombatRecorderSampleIntervalMs, 30, 50);
        var now = Environment.TickCount64;
        if (now - _lastCacheAt < interval) return;
        _lastCacheAt = now;

        var snapshot = CaptureSnapshot(CombatRecorderEventType.Unknown, 0, success: null);
        snapshot.SampleRole = "cache";
        _frames.Push(snapshot);
    }

    public int ClearOldLogs(int keepDays)
    {
        if (!Directory.Exists(LogRoot)) return 0;
        var cutoff = DateTime.Now.AddDays(-Math.Max(1, keepDays));
        var deleted = 0;

        foreach (var file in Directory.GetFiles(LogRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime >= cutoff) continue;
                info.Delete();
                deleted++;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[CombatRecorder] 清理日志失败 {file}: {ex.Message}");
            }
        }

        return deleted;
    }

    public void OpenLogDirectory()
    {
        Directory.CreateDirectory(LogRoot);
        try { Process.Start("explorer.exe", LogRoot); }
        catch (Exception ex) { DService.Instance().Log.Warning($"[CombatRecorder] 打开日志目录失败: {ex.Message}"); }
    }

    private void OnUseAction(EventSystem.UseActionEvent ev)
    {
        if (ev is not { Result: true, ActionId: not 0, ActionType: ActionTypeFF.Action })
            return;

        var isAbilityAction = IsAbilityAction(ev.ActionId);
        if (!ShouldCaptureEvent(CombatRecorderEventType.UseActionSuccess, isAbilityAction, gcdJustActivated: false))
            return;

        CaptureEvent(CombatRecorderEventType.UseActionSuccess, ev.ActionId, success: true);
    }

    private void OnGameEvent(ITriggerCondParams p)
    {
        switch (p)
        {
            case SelfCastStartCondParams sc when IsSelfSource(sc.SourceID):
                if (IsAbilityAction(sc.SpellId)) break;
                _pendingCastGcdActionId = sc.SpellId;
                _pendingCastGcdRecordedAt = sc.StartCastTime;
                CaptureEvent(CombatRecorderEventType.GcdReadyAndAction, sc.SpellId, success: true);
                break;
            case ReceviceAbilityEffectCondParams ae when IsSelfSource(ae.SourceID):
                if (IsDuplicateEffect(ae.ActionId)) break;
                if (IsAbilityAction(ae.ActionId))
                {
                    if (!ShouldCaptureEvent(CombatRecorderEventType.AbilityEffect, isAbilityAction: true, gcdJustActivated: false)) break;
                    CaptureEvent(CombatRecorderEventType.AbilityEffect, ae.ActionId, success: true);
                    break;
                }

                if (!ShouldCaptureGcdEffect(ae.ActionId, _pendingCastGcdActionId, _pendingCastGcdRecordedAt, Environment.TickCount64)) break;
                CaptureEvent(CombatRecorderEventType.GcdReadyAndAction, ae.ActionId, success: true);
                break;
            case ReceviceNoTargetAbilityEffectCondParams ne when IsSelfSource(ne.SourceID):
                if (IsDuplicateEffect(ne.ActionId)) break;
                if (IsAbilityAction(ne.ActionId))
                {
                    if (!ShouldCaptureEvent(CombatRecorderEventType.AbilityEffect, isAbilityAction: true, gcdJustActivated: false)) break;
                    CaptureEvent(CombatRecorderEventType.AbilityEffect, ne.ActionId, success: true);
                    break;
                }

                if (!ShouldCaptureGcdEffect(ne.ActionId, _pendingCastGcdActionId, _pendingCastGcdRecordedAt, Environment.TickCount64)) break;
                CaptureEvent(CombatRecorderEventType.GcdReadyAndAction, ne.ActionId, success: true);
                break;
        }
    }

    private void CaptureEvent(CombatRecorderEventType eventType, uint actionId, bool? success, bool cancelled = false)
    {
        if (!IsEnabledForCurrentState()) return;

        var current = CaptureSnapshot(eventType, actionId, success, cancelled);
        var previous = _frames.GetLatest();
        var eventGroupId = $"{_sessionId}-{Interlocked.Increment(ref _sampleIndex):D8}";
        var samples = CombatRecorderEventWriter.BuildEventSamples(
            previous,
            current,
            PluginConfig.Instance.CombatRecorderIncludePreviousFrame,
            eventGroupId);

        WriteSamples(samples);
    }

    private CombatFrameSnapshot CaptureSnapshot(
        CombatRecorderEventType eventType,
        uint actionId,
        bool? success,
        bool cancelled = false)
    {
        EnsureSession();

        var self = Data.Me.Object as IBattleChara;
        var target = Data.Target.Current;
        var targetBattle = target as IBattleChara;
        var source = GetConfiguredSource();
        var includeBuffs = PluginConfig.Instance.CombatRecorderIncludeBuffDetails;

        var snapshot = new CombatFrameSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            FrameIndex = Interlocked.Increment(ref _frameIndex),
            SampleIndex = _sampleIndex,
            SessionId = _sessionId,
            EventType = eventType.ToString(),
            Source = source,
            Job = Data.Me.ClassJob,
            ActionId = actionId,
            ActionName = GetActionName(actionId),
            QueuedActionId = ReadQueuedActionId(),
            CastActionId = self?.CastActionID ?? 0,
            IsAbility = actionId != 0 && IsAbilityAction(actionId),
            Success = success,
            Failed = success == false && !cancelled,
            Cancelled = cancelled,
            Hp = self?.CurrentHp ?? 0,
            MaxHp = self?.MaxHp ?? 0,
            Mp = self?.CurrentMp ?? 0,
            MaxMp = self?.MaxMp ?? 0,
            Level = Data.Me.CurrentLevel,
            InCombat = Data.Combat.InCombat,
            IsMoving = Data.Me.IsMoving,
            IsCasting = self?.IsCasting ?? Data.Combat.IsCasting,
            TotalCastTime = self?.TotalCastTime ?? 0,
            CurrentCastTime = self?.CurrentCastTime ?? 0,
            GcdCooldown = GCDHelper.GetGCDCooldown(),
            GcdDuration = GCDHelper.GetGCDDuration(),
            LastComboSpellId = EventSystem.LastComboSpellId,
            TargetId = target?.EntityID ?? 0,
            TargetName = target?.Name ?? "",
            Distance = target != null ? Data.Me.DistanceToObject2D(target) : 0,
            IsBoss = targetBattle?.MaxHp > 100000,
            IsDead = target?.IsDead ?? false,
            IsTargetable = target?.IsTargetable ?? false,
            TargetHp = targetBattle?.CurrentHp ?? 0,
            TargetMaxHp = targetBattle?.MaxHp ?? 0,
            QtStates = QTHelper.GetAll().ToDictionary(q => q.Id, q => q.Value),
            CurrentAcrName = ACRLifecycle.CurrentAcrName,
            CurrentRotationName = ACRLifecycle.Runner.CurrentRotation?.GetType().Name ?? "",
            JobGauge = CaptureJobGauge(self),
            ResolverResults = CaptureResolvers()
        };

        snapshot.IsGcd = actionId != 0 && !snapshot.IsAbility;
        snapshot.HpPercent = Percent(snapshot.Hp, snapshot.MaxHp);
        snapshot.MpPercent = Percent(snapshot.Mp, snapshot.MaxMp);
        snapshot.TargetHpPercent = Percent(snapshot.TargetHp, snapshot.TargetMaxHp);

        if (includeBuffs)
        {
            snapshot.SelfBuffs = CaptureBuffs(self);
            snapshot.TargetBuffs = CaptureBuffs(targetBattle);
        }

        FillEnemyCounts(snapshot, self, target);
        return snapshot;
    }

    private List<CombatResolverSnapshot> CaptureResolvers()
    {
        if (!PluginConfig.Instance.CombatRecorderIncludeResolverResults) return [];
        return ACRLifecycle.Runner.Debug.Resolvers.Select(r => new CombatResolverSnapshot
        {
            Name = r.Name,
            Mode = r.Mode,
            CheckResult = r.CheckResult,
            PassedWindow = r.PassedWindow
        }).ToList();
    }

    private static List<CombatBuffSnapshot> CaptureBuffs(IBattleChara? chara)
    {
        if (chara == null) return [];

        var result = new List<CombatBuffSnapshot>(chara.StatusList.Length);
        foreach (var status in chara.StatusList)
        {
            if (status.StatusID == 0) continue;
            var name = "";
            try { name = status.GameData.Value.Name.ToString(); }
            catch { }
            result.Add(new CombatBuffSnapshot
            {
                Id = status.StatusID,
                Name = name,
                Stack = status.Param,
                RemainMs = Math.Max(0, (int)(status.RemainingTime * 1000f))
            });
        }
        return result;
    }

    private static Dictionary<string, object?> CaptureJobGauge(IBattleChara? self)
    {
        var gauge = new Dictionary<string, object?>();

        if (Data.Me.ClassJob != (uint)Jobs.BLM || self == null)
            return gauge;

        try
        {
            var blm = DService.Instance().JobGauges.Get<BLMGauge>();
            gauge["inAstralFire"] = blm.InAstralFire;
            gauge["astralFireStacks"] = blm.AstralFireStacks;
            gauge["inUmbralIce"] = blm.InUmbralIce;
            gauge["umbralIceStacks"] = blm.UmbralIceStacks;
            gauge["umbralHearts"] = blm.UmbralHearts;
            gauge["astralSoulStacks"] = blm.AstralSoulStacks;
            gauge["polyglotStacks"] = blm.PolyglotStacks;
            gauge["isParadoxActive"] = blm.IsParadoxActive;
            gauge["enochianTimer"] = TryGetRemainMs(self, 737, out var timerMs) ? timerMs : 0;
            return gauge;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Debug($"[CombatRecorder] BLM Gauge 读取失败，使用 Buff 推导: {ex.Message}");
        }

        var astralFireStacks = GetStack(self, 173);
        var umbralIceStacks = GetStack(self, 174);
        var enochian = TryGetRemainMs(self, 737, out var enochianMs);
        gauge["inAstralFire"] = astralFireStacks > 0;
        gauge["astralFireStacks"] = astralFireStacks;
        gauge["inUmbralIce"] = umbralIceStacks > 0;
        gauge["umbralIceStacks"] = umbralIceStacks;
        gauge["umbralHearts"] = GetStack(self, 264);
        gauge["astralSoulStacks"] = GetStack(self, 3870);
        gauge["polyglotStacks"] = GetStack(self, 1211);
        gauge["isParadoxActive"] = self.StatusList.HasStatus(3097);
        gauge["enochianTimer"] = enochian ? enochianMs : 0;
        return gauge;
    }

    private static ushort GetStack(IBattleChara self, uint statusId)
    {
        return self.StatusList.TryGetStatus(statusId, out var status, out _) ? status?.Param ?? (ushort)1 : (ushort)0;
    }

    private static bool TryGetRemainMs(IBattleChara self, uint statusId, out int remainMs)
    {
        if (self.StatusList.TryGetStatus(statusId, out var status, out _) && status != null)
        {
            remainMs = Math.Max(0, (int)(status.RemainingTime * 1000f));
            return true;
        }
        remainMs = 0;
        return false;
    }

    private static void FillEnemyCounts(CombatFrameSnapshot snapshot, IBattleChara? self, IGameObject? target)
    {
        foreach (var enemy in Data.Objects.Enemies)
        {
            if (enemy is not IBattleChara bc || bc.IsDead || !bc.IsTargetable) continue;

            if (self != null)
            {
                var distSelf = Data.Me.DistanceToObject2D(bc);
                if (distSelf <= 5) snapshot.EnemyCountNearSelf5++;
                if (distSelf <= 10) snapshot.EnemyCountNearSelf10++;
                if (distSelf <= 15) snapshot.EnemyCountNearSelf15++;
                if (distSelf <= 25) snapshot.EnemyCountNearSelf25++;
            }

            if (target != null)
            {
                var distTarget = Distance2D(target, bc);
                if (distTarget <= 5) snapshot.EnemyCountNearTarget5++;
                if (distTarget <= 10) snapshot.EnemyCountNearTarget10++;
                if (distTarget <= 15) snapshot.EnemyCountNearTarget15++;
                if (distTarget <= 25) snapshot.EnemyCountNearTarget25++;
            }
        }
    }

    private static float Distance2D(IGameObject a, IGameObject b)
    {
        var dx = a.Position.X - b.Position.X;
        var dz = a.Position.Z - b.Position.Z;
        return Math.Max(0, MathF.Sqrt(dx * dx + dz * dz) - a.HitboxRadius - b.HitboxRadius);
    }

    private void WriteSamples(IReadOnlyList<CombatFrameSnapshot> samples)
    {
        if (samples.Count == 0) return;

        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath)!);
            using var writer = new StreamWriter(_sessionPath, append: true);
            foreach (var sample in samples)
            {
                writer.WriteLine(JsonSerializer.Serialize(sample, JsonOptions));
            }
        }
    }

    private void EnsureSession()
    {
        if (!string.IsNullOrEmpty(_sessionId) && !string.IsNullOrEmpty(_sessionPath))
            return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        _sessionId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{suffix}";
        var job = ((Jobs)Data.Me.ClassJob).ToString();
        var dir = Path.Combine(LogRoot, job, DateTime.Now.ToString("yyyy-MM-dd"));
        _sessionPath = Path.Combine(dir, $"session-{_sessionId}.jsonl");
    }

    private bool IsEnabledForCurrentState()
    {
        var cfg = PluginConfig.Instance;
        if (!cfg.CombatRecorderEnabled) return false;
        if (!HiAuRo.Data.IsReady) return false;
        if (cfg.CombatRecorderOnlyInCombat && !Data.Combat.InCombat) return false;
        if (cfg.CombatRecorderOnlyCurrentJob
            && ACRLifecycle.CurrentJobId != 0
            && Data.Me.ClassJob != ACRLifecycle.CurrentJobId)
            return false;
        return Data.Me.Object != null;
    }

    private void ApplyManualSourcePause()
    {
        var shouldPause = PluginConfig.Instance.CombatRecorderEnabled
            && PluginConfig.Instance.CombatRecorderSourceMode == CombatRecorderSourceMode.Manual;

        if (!shouldPause)
        {
            if (_lastManualPauseApplied && MainControlHelper.IsPaused)
                MainControlHelper.Unpause();
            _lastManualPauseApplied = false;
            return;
        }

        if (!MainControlHelper.IsPaused)
        {
            MainControlHelper.Pause();
            _lastManualPauseApplied = true;
        }
    }

    private static string GetConfiguredSource()
    {
        return PluginConfig.Instance.CombatRecorderSourceMode switch
        {
            CombatRecorderSourceMode.Manual => "manual",
            CombatRecorderSourceMode.Acr => "acr",
            _ => "unknown"
        };
    }

    private static bool IsSelfSource(uint sourceId) => sourceId == (Data.Me.Object?.EntityID ?? 0);

    internal static bool ShouldCaptureEventForTests(
        CombatRecorderEventType eventType,
        bool isAbilityAction,
        bool gcdJustActivated)
        => ShouldCaptureEvent(eventType, isAbilityAction, gcdJustActivated);

    internal static bool ShouldCaptureGcdEffectForTests(
        uint actionId,
        uint pendingCastGcdActionId,
        long pendingCastRecordedAtTicks,
        long nowTicks)
        => ShouldCaptureGcdEffect(actionId, pendingCastGcdActionId, pendingCastRecordedAtTicks, nowTicks);

    private static bool ShouldCaptureEvent(
        CombatRecorderEventType eventType,
        bool isAbilityAction,
        bool gcdJustActivated)
    {
        return eventType switch
        {
            CombatRecorderEventType.UseActionSuccess => false,
            CombatRecorderEventType.AbilityEffect => isAbilityAction,
            CombatRecorderEventType.GcdReadyAndAction => !isAbilityAction && gcdJustActivated,
            _ => false
        };
    }

    private static bool ShouldCaptureGcdEffect(
        uint actionId,
        uint pendingCastGcdActionId,
        long pendingCastRecordedAtTicks,
        long nowTicks)
    {
        if (pendingCastGcdActionId == 0)
            return true;

        if (pendingCastGcdActionId != actionId)
            return true;

        return nowTicks - pendingCastRecordedAtTicks > 3000;
    }

    private void ResetTransientState()
    {
        _pendingCastGcdActionId = 0;
        _pendingCastGcdRecordedAt = 0;
        _lastEffectActionId = 0;
        _lastEffectAt = 0;
    }

    private bool IsDuplicateEffect(uint actionId)
    {
        var now = Environment.TickCount64;
        if (_lastEffectActionId == actionId && now - _lastEffectAt < 200)
            return true;
        _lastEffectActionId = actionId;
        _lastEffectAt = now;
        return false;
    }

    private static float Percent(uint value, uint max) => max > 0 ? (float)value / max : 0f;

    private static unsafe uint ReadQueuedActionId()
    {
        var am = ActionManager.Instance();
        return am == null ? 0 : am->QueuedActionId;
    }

    private static string GetActionName(uint actionId)
    {
        if (actionId == 0) return "";
        return OmenTools.Interop.Game.Lumina.LuminaWrapper.GetActionName(actionId);
    }

    private static unsafe bool IsAbilityAction(uint actionId)
    {
        if (actionId == 0) return false;

        var row = SpellHelper.GetActionRow(actionId);
        if (row.HasValue)
        {
            var cat = row.Value.ActionCategory.RowId;
            if (cat == 4) return true;
            if (cat is 2 or 3) return false;
        }

        var am = ActionManager.Instance();
        if (am == null) return false;
        var gcdGroup = am->GetRecastGroup((int)ActionTypeFF.Action, 9);
        var actionGroup = am->GetRecastGroup((int)ActionTypeFF.Action, actionId);
        return actionGroup != 0 && actionGroup != gcdGroup;
    }
}
