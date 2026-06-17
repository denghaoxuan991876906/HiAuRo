using System.Text.Json;
using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using HiAuRo.Infrastructure;

namespace HiAuRo.FactAxis;

/// <summary>
/// 事实轴 — 阶段内纯时间推进，Sync 校准事件开始/结束
/// </summary>
public sealed class FactTimeline
{
    /// <summary>事实轴单例</summary>
    public static FactTimeline Instance { get; } = new();

    /// <summary>时间线数据</summary>
    public FactTimelineData? Data { get; private set; }
    /// <summary>是否已初始化</summary>
    public bool Initialized { get; private set; }
    /// <summary>是否在运行</summary>
    public bool IsRunning { get; private set; }
    /// <summary>运行时状态</summary>
    public FactState State { get; } = new();

    /// <summary>阶段切换事件（phaseId, phaseName）。EnterPhase 和 TrySwitchBranch 中触发。</summary>
    public event Action<string, string>? PhaseChanged;

    private readonly Dictionary<string, bool> _variables = [];
    private long _timebase;
    private double _phaseStartTime;
    private readonly List<(FactSyncDef Sync, FactEvent Event)> _activeSyncs = [];
    private int _nextSyncEnd;
    private FactPhase? _currentPhase;
    private List<FactEvent> _currentEvents = [];
    private int _eventIndex;
    private FactPhaseSwitch? _pendingSwitch;
    private FactEvent? _waitingStartSync;  // 等待开始 Sync 的事件
    private FactEvent? _waitingEndSync;    // 等待结束 Sync 的事件
    private bool _waitingSwitch;
    // QT offset 延迟执行
    private readonly List<(FactAction Action, double ExecuteAt)> _pendingQtActions = new();
    private uint _previousTerritoryId;

    private FactTimeline() { }

    private double _cachedFightNow;
    private double FightNow => _cachedFightNow;

    private void UpdateFightNow()
    {
        _cachedFightNow = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _timebase) / 1000.0;
    }

    #region 生命周期

    /// <summary>初始化事实轴</summary>
    public void Init()
    {
        if (Initialized) return;
        Initialized = true;
        AutoLoadTimeline();
    }

    /// <summary>启动事实轴时间线</summary>
    public void Start()
    {
        if (!Initialized || Data == null) return;
        if (IsRunning) return;
        Reset();
        IsRunning = true;

        _timebase = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UpdateFightNow();

        BuildSyncWindows();

        if (Data.Phases.Count > 0)
            EnterPhase(Data.Phases[0]);

        GameEventHook.Instance.OnEventFired += OnGameEvent;

        DService.Instance().Log.Information($"[FactAxis] 启动 (timebase): {Data.Name}");
    }

    /// <summary>停止事实轴时间线</summary>
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        GameEventHook.Instance.OnEventFired -= OnGameEvent;
        UpdateFightNow();
        DService.Instance().Log.Information($"[FactAxis] 停止: {State.PhaseName}, 战斗 {_cachedFightNow:F1}s");
    }

    /// <summary>完全关闭事实轴</summary>
    public void Shutdown()
    {
        GameEventHook.Instance.OnEventFired -= OnGameEvent;
        Stop();
        Reset();
        Data = null;
        Initialized = false;
    }

    private void Reset()
    {
        _timebase = 0;
        _phaseStartTime = 0;
        _activeSyncs.Clear();
        _nextSyncEnd = 0;
        _currentPhase = null;
        _currentEvents = [];
        _eventIndex = 0;
        _pendingSwitch = null;
        _waitingStartSync = null;
        _waitingEndSync = null;
        _waitingSwitch = false;
        _pendingQtActions.Clear();
        _variables.Clear();
        State.Clear();

        if (Data != null)
        {
            foreach (var p in Data.Phases)
                ResetPhase(p);
        }
    }

    private static void ResetPhase(FactPhase phase)
    {
        foreach (var e in phase.Events) ResetEvent(e);
        if (phase.Switch != null)
        {
            foreach (var b in phase.Switch.Branches)
            {
                foreach (var e in b.Events) ResetEvent(e);
                if (b.Switch != null)
                {
                    foreach (var bb in b.Switch.Branches)
                        foreach (var e in bb.Events) ResetEvent(e);
                }
            }
        }
    }

    private static void ResetEvent(FactEvent e)
    {
        e.Reached = false;
        e.ActualStart = 0;
        e.ActualEnd = 0;
        e.ActionsDone = false;
        e.SyncFired = false;
    }

    #endregion

    #region 阶段与分支

    private void EnterPhase(FactPhase phase)
    {
        _currentPhase = phase;
        _currentEvents = phase.Events;
        _eventIndex = 0;
        _pendingSwitch = phase.Switch;
        _waitingSwitch = false;
        _phaseStartTime = FightNow;

        PhaseChanged?.Invoke(phase.Id, phase.Name);
        DService.Instance().Log.Debug($"[FactAxis] 进入阶段: {phase.Name} ({phase.Events.Count} 事件)");
    }

    /// <summary>
    /// Sync 触发 → 评估分支 → 选中第一个满足条件的分支 → 替换事件列表 + 切换点
    /// </summary>
    private bool TrySwitchBranch()
    {
        if (_pendingSwitch == null || _pendingSwitch.Branches.Count == 0)
            return false;

        // 执行切换点动作
        foreach (var action in _pendingSwitch.Actions)
        {
            try { action.Execute(this); }
            catch (Exception ex) { DService.Instance().Log.Error($"[FactAxis] 切换动作异常: {ex}"); }
        }

        // 找第一个满足条件的分支
        FactSwitchBranch? selected = null;
        foreach (var branch in _pendingSwitch.Branches)
        {
            if (branch.Condition == null || branch.Condition.Evaluate(GetVariable))
            {
                selected = branch;
                break;
            }
        }

        if (selected == null)
        {
            DService.Instance().Log.Warning($"[FactAxis] 无匹配分支，停在当前状态");
            return false;
        }

        DService.Instance().Log.Debug($"[FactAxis] 分支切换: {selected.Name}");

        // 替换事件列表
        _currentEvents = selected.Events;
        foreach (var e in _currentEvents) ResetEvent(e);
        _eventIndex = 0;
        _pendingSwitch = selected.Switch;
        _waitingSwitch = false;
        _phaseStartTime = FightNow;

        PhaseChanged?.Invoke(_currentPhase?.Id ?? "", selected.Name);
        return true;
    }

    #endregion

    #region 事件订阅

    private void OnGameEvent(ITriggerCondParams e)
    {
        if (!IsRunning) return;
        UpdateFightNow();
        var fightNow = FightNow;

        switch (e)
        {
            case EnemyCastSpellCondParams cast:
                MatchActiveSyncs("startsUsing", cast.SpellId, fightNow);
                break;

            case ReceviceAbilityEffectCondParams effect:
                MatchActiveSyncs("ability", effect.ActionId, fightNow);
                break;

            case ReceviceNoTargetAbilityEffectCondParams effect:
                MatchActiveSyncs("ability", effect.ActionId, fightNow);
                break;

            case TargetIconEffectTestCondParams icon:
                MatchActiveSyncs("headMarker", icon.IconId, fightNow);
                break;

            case TetherCreateParams tether:
                MatchActiveSyncs("tether", tether.TetherID, fightNow);
                break;

            case TetherRemoveParams tether:
                // 连线移除不带 TetherID，无法做 Sync 匹配
                break;

            case ActorDeathParams death:
                MatchActiveSyncs("wasDefeated", death.SourceID, fightNow);
                break;

            case AddStatusCondParams buff:
                MatchActiveSyncs("gainsEffect", buff.StatusId, fightNow);
                break;

            case RemoveStatusCondParams buff:
                MatchActiveSyncs("losesEffect", buff.StatusId, fightNow);
                break;

            case OnMapEffectCreateEvent map:
                MatchActiveSyncs("mapEffect", map.Pos, fightNow);
                break;

            case NpcYellCondParams yell:
                MatchActiveSyncs("npcYell", (uint)yell.yellId, fightNow);
                break;

            case UnitCreateCondParams create:
                MatchActiveSyncs("addedCombatant", create.DataId, fightNow);
                break;

            case UnitDeleteCondParams delete:
                MatchActiveSyncs("removedCombatant", delete.DataId, fightNow);
                break;

            case OnEnvControlEvent env:
                MatchActiveSyncs("envControl", env.Index, fightNow);
                break;

            case WeatherChangedCondParams weather:
                MatchActiveSyncs("weather", (uint)weather.NewWeatherId, fightNow);
                break;

            case ObjectChangeParams objChange:
                MatchActiveSyncs("objectChange", objChange.Command, fightNow);
                break;

            case ObjectEffectParams objEffect:
                MatchActiveSyncs("objectEffect", objEffect.Data1, fightNow);
                break;

            case ObjectSpawnParams objSpawn:
                MatchActiveSyncs("objectSpawn", objSpawn.DataId, fightNow);
                break;

            // 以下类型暂无对应的 FactEventType，仅接收不做 Sync 匹配
            case TargetAbleCondParams:
            case ActorControlCombatParams:
            case PlayActionTimelineParams:
            case ActorControlParams:
            case DirectorUpdateParams:
            case AfterSpellCondParams:
            case CombatStateParams:
                break;
        }
    }

    #endregion

    #region 变量

    /// <summary>获取变量值</summary>
    public bool GetVariable(string name) => _variables.TryGetValue(name, out var v) && v;
    /// <summary>设置变量值</summary>
    public void SetVariable(string name, bool value) => _variables[name] = value;
    /// <summary>切换变量值</summary>
    public void ToggleVariable(string name) => _variables[name] = !GetVariable(name);

    #endregion

    #region 每帧更新

    /// <summary>每帧更新事实轴时间线</summary>
    public FactState Update(int battleTimeMs)
    {
        if (!Initialized || Data == null || !IsRunning)
        {
            State.IsRunning = false;
            return State;
        }

        UpdateFightNow();

        // 副本切换
        var territory = OmenTools.OmenService.GameState.TerritoryType;
        if (territory != 0 && territory != _previousTerritoryId)
        {
            _previousTerritoryId = territory;
            AutoLoadTimeline();
        }

        return BuildState();
    }

    /// <summary>构建当前状态快照</summary>
    private FactState BuildState()
    {
        var fightNow = FightNow;

        // 检查到期 pending QT actions
        for (int i = _pendingQtActions.Count - 1; i >= 0; i--)
        {
            var (qtAction, executeAt) = _pendingQtActions[i];
            if (fightNow >= executeAt)
            {
                if (PluginConfig.Instance.FactAxis.QtControl)
                {
                    if (qtAction is 切换QT动作 toggleAction)
                        QTHelper.Toggle(toggleAction.QtId);
                    else if (qtAction is 设置QT动作 setAction)
                        QTHelper.SetValue(setAction.QtId, setAction.Value);
                }
                _pendingQtActions.RemoveAt(i);
            }
        }

        double phaseTime = _phaseStartTime > 0 ? fightNow - _phaseStartTime : fightNow;

        // 检查 forcejump：时间到即跳
        if (_waitingStartSync?.StartSync is { ForceJump: true } sync
            && fightNow >= sync.AnchorTime)
        {
            SyncTo(sync.ForceJumpTarget ?? sync.AnchorTime);
            return BuildState();
        }

        State.IsRunning = true;
        State.TimelineName = Data?.Name ?? "";
        State.PhaseName = _currentPhase?.Name ?? "";
        State.PhaseTime = phaseTime;
        State.TotalTime = fightNow;

        // 就地更新 Variables（复用已有 Dictionary 容量）
        State.Variables.Clear();
        foreach (var kv in _variables)
            State.Variables[kv.Key] = kv.Value;

        if (_waitingStartSync == null && _waitingEndSync == null && !_waitingSwitch)
        {
            AdvanceTimedEvents(fightNow);
        }

        // 就地更新 PendingEvents（复用已有 List 容量）
        State.PendingEvents.Clear();
        for (int i = _eventIndex; i < _currentEvents.Count; i++)
            State.PendingEvents.Add(_currentEvents[i]);

        CollectActiveWindows(fightNow);

        if (_waitingSwitch)
            State.Status = "waiting_sync";
        else if (_waitingStartSync != null)
            State.Status = $"waiting_start: {_waitingStartSync.Name}";
        else if (_waitingEndSync != null)
            State.Status = $"waiting_end: {_waitingEndSync.Name}";
        else
            State.Status = "running";

        State.CurrentEvent = _waitingStartSync ?? _waitingEndSync;
        State.NextEventTime = GetNextEventTime(fightNow);

        State.Suggestions.Clear();
        var next = GetNextEvent(fightNow);
        if (next != null)
        {
            foreach (var action in next.Actions)
            {
                if (action is SkillSuggestionAction sug)
                    State.Suggestions.Add(sug);
            }
        }

        return State;
    }

    /// <summary>推进到当前时间的下一个事件</summary>
    private void AdvanceTimedEvents(double fightNow)
    {
        while (_eventIndex < _currentEvents.Count)
        {
            var ev = _currentEvents[_eventIndex];

            if (!ev.Reached && fightNow >= ev.Time)
            {
                if (ev.StartSync != null)
                {
                    _waitingStartSync = ev;
                    _eventIndex++;
                    break;
                }
                else
                {
                    ev.ActualStart = fightNow;
                    RunActions(ev);
                    if (ev.Targetable != null)
                        State.IsTargetable = ev.Targetable.Value;
                    ev.Reached = true;

                    if (ev.Duration.HasValue && ev.Duration.Value > 0)
                        ev.ActualEnd = ev.ActualStart + ev.Duration.Value;
                    else
                        ev.ActualEnd = ev.ActualStart;
                }
            }

            _eventIndex++;
        }

        if (_eventIndex >= _currentEvents.Count && _pendingSwitch != null)
            _waitingSwitch = true;
    }

    private void RunActions(FactEvent ev)
    {
        if (ev.ActionsDone) return;
        foreach (var action in ev.Actions)
        {
            try
            {
                switch (action)
                {
                    case 设置QT动作 qtSet when qtSet.Offset != 0:
                        _pendingQtActions.Add((qtSet, ev.Time + qtSet.Offset));
                        break;
                    case 设置QT动作 qtSet:
                        if (PluginConfig.Instance.FactAxis.QtControl)
                            QTHelper.SetValue(qtSet.QtId, qtSet.Value);
                        break;
                    case 切换QT动作 qtToggle when qtToggle.Offset != 0:
                        _pendingQtActions.Add((qtToggle, ev.Time + qtToggle.Offset));
                        break;
                    case 切换QT动作 qtToggle:
                        if (PluginConfig.Instance.FactAxis.QtControl)
                            QTHelper.Toggle(qtToggle.QtId);
                        break;
                    default:
                        action.Execute(this);
                        break;
                }
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[FactAxis] 动作异常: {ex}"); }
        }
        ev.ActionsDone = true;
    }

    #region Sync 窗口系统

    private void BuildSyncWindows()
    {
        _activeSyncs.Clear();
        _nextSyncEnd = 0;

        foreach (var phase in Data!.Phases)
            BuildPhaseSyncWindows(phase);
    }

    private void BuildPhaseSyncWindows(FactPhase phase)
    {
        foreach (var e in phase.Events)
        {
            if (e.StartSync != null)
            {
                e.StartSync.AnchorTime = e.Time;
                e.StartSync.Start = e.Time - e.StartSync.WindowBefore;
                e.StartSync.End = e.Time + e.StartSync.WindowAfter;
                _activeSyncs.Add((e.StartSync, e));
            }
        }
        if (phase.Switch != null)
        {
            phase.Switch.Sync.AnchorTime = double.MaxValue;
            phase.Switch.Sync.Start = 0;
            phase.Switch.Sync.End = double.MaxValue;
            _activeSyncs.Add((phase.Switch.Sync, null!)); // Switch sync 无关联 FactEvent
        }
        _activeSyncs.Sort((a, b) => a.Sync.Start.CompareTo(b.Sync.Start));
    }

    private void CollectActiveWindows(double fightNow)
    {
        while (_nextSyncEnd < _activeSyncs.Count)
        {
            var sync = _activeSyncs[_nextSyncEnd].Sync;
            if (sync.Start <= fightNow)
                _nextSyncEnd++;
            else
                break;
        }
    }

    private void MatchActiveSyncs(string gameEventType, uint abilityId, double fightNow)
    {
        for (int i = 0; i < _nextSyncEnd; i++)
        {
            var (sync, ev) = _activeSyncs[i];
            if (sync.Start > fightNow) break;
            if (sync.End <= fightNow) continue;

            // Switch sync — 跳过（由 TrySwitchBranch 单独处理）
            if (ev == null) continue;

            if (ev.Type == FactEventType.None) continue;

            var evTypeName = ev.Type switch
            {
                FactEventType.Ability           => "ability",
                FactEventType.StartsUsing       => "startsUsing",
                FactEventType.HeadMarker        => "headMarker",
                FactEventType.Tether            => "tether",
                FactEventType.AddedCombatant    => "addedCombatant",
                FactEventType.RemovedCombatant  => "removedCombatant",
                FactEventType.WasDefeated       => "wasDefeated",
                FactEventType.GainsEffect       => "gainsEffect",
                FactEventType.LosesEffect       => "losesEffect",
                FactEventType.MapEffect         => "mapEffect",
                FactEventType.NPCYell           => "npcYell",
                FactEventType.EnvControl        => "envControl",
                FactEventType.Weather           => "weather",
                FactEventType.ObjectChange      => "objectChange",
                FactEventType.ObjectEffect      => "objectEffect",
                FactEventType.ObjectSpawn       => "objectSpawn",
                _                               => null
            };
            if (evTypeName == null || evTypeName != gameEventType) continue;
            if (ev.AbilityId != 0 && ev.AbilityId != abilityId) continue;

            var targetTime = sync.Jump ?? sync.AnchorTime;
            if (targetTime >= double.MaxValue - 1) continue;

            SyncTo(targetTime);
            return;
        }
    }

    #endregion

    #region Sync 校准

    private void SyncTo(double eventTime)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var newTimebase = now - (long)(eventTime * 1000);

        if (Math.Abs(newTimebase - _timebase) <= 2) return;

        var oldDelta = (newTimebase - _timebase) / 1000.0;
        _timebase = newTimebase;
        UpdateFightNow();

        DService.Instance().Log.Debug(
            $"[FactAxis] SyncTo: eventTime={eventTime:F2}s, drift={oldDelta:F3}s");

        AdvancePastExpired(FightNow);
        _nextSyncEnd = 0;
        CollectActiveWindows(FightNow);
    }

    private void AdvancePastExpired(double fightNow)
    {
        _eventIndex = 0;
        _waitingStartSync = null;
        _waitingEndSync = null;
        _waitingSwitch = false;

        while (_eventIndex < _currentEvents.Count)
        {
            var ev = _currentEvents[_eventIndex];
            if (ev.Time > fightNow) break;
            _eventIndex++;
        }

        if (_eventIndex >= _currentEvents.Count && _pendingSwitch != null)
            _waitingSwitch = true;
    }

    #endregion

    private double? GetNextEventTime(double fightNow)
    {
        for (int i = _eventIndex; i < _currentEvents.Count; i++)
        {
            if (!_currentEvents[i].Reached && _currentEvents[i].Time > fightNow)
                return _currentEvents[i].Time;
        }
        return null;
    }

    private FactEvent? GetNextEvent(double fightNow)
    {
        for (int i = _eventIndex; i < _currentEvents.Count; i++)
        {
            if (!_currentEvents[i].Reached && _currentEvents[i].Time > fightNow)
                return _currentEvents[i];
        }
        return null;
    }

    #endregion

    #region 加载

    /// <summary>从 JSON 字符串加载时间线</summary>
    public bool LoadFromJson(string json)
    {
        try
        {
            Data = JsonSerializer.Deserialize<FactTimelineData>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (Data == null) return false;
            MigrateTimelineData(Data);
            DService.Instance().Log.Information($"[FactAxis] 已加载: {Data.Name} ({Data.Phases.Count} 阶段)");
            return true;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[FactAxis] JSON 解析失败: {ex}");
            return false;
        }
    }

    /// <summary>从文件加载时间线</summary>
    public bool LoadFromFile(string path)
    {
        if (!File.Exists(path)) return false;
        return LoadFromJson(File.ReadAllText(path));
    }

    /// <summary>自动加载当前副本的时间线</summary>
    public void AutoLoadTimeline()
    {
        var tid = OmenTools.OmenService.GameState.TerritoryType;
        if (tid == 0) return;
        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "FactTimelines");
        var path = Path.Combine(dir, $"{tid}.json");
        if (File.Exists(path)) LoadFromFile(path);
    }

    private static void MigrateTimelineData(FactTimelineData data)
    {
        foreach (var phase in data.Phases)
            MigratePhaseEvents(phase);
    }

    private static void MigratePhaseEvents(FactPhase phase)
    {
        foreach (var ev in phase.Events)
            ev.MigrateFromLegacy();
        if (phase.Switch != null)
        {
            foreach (var branch in phase.Switch.Branches)
            {
                foreach (var ev in branch.Events)
                    ev.MigrateFromLegacy();
                if (branch.Switch != null)
                {
                    foreach (var bb in branch.Switch.Branches)
                        foreach (var ev in bb.Events)
                            ev.MigrateFromLegacy();
                }
            }
        }
    }

    #endregion
}
