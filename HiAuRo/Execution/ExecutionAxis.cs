using System.Collections.Concurrent;
using HiAuRo.ACR;

namespace HiAuRo.Execution;

/// <summary>
/// 执行轴 — 异步触发树驱动
/// </summary>
public sealed class ExecutionAxis
{
    /// <summary>执行轴单例</summary>
    public static ExecutionAxis Instance { get; } = new();

    /// <summary>根节点</summary>
    public TriggerCompositeNode? Root { get; private set; }
    /// <summary>求值上下文</summary>
    public EvalContext Context { get; } = new();
    /// <summary>当前执行输出</summary>
    public ExecutionOutput CurrentOutput { get; } = new();
    /// <summary>是否正在运行</summary>
    public bool IsRunning { get; private set; }
    /// <summary>是否已初始化</summary>
    public bool Initialized { get; private set; }
    /// <summary>时间线名称</summary>
    public string TimelineName { get; private set; } = "";
    /// <summary>副本 ID</summary>
    public uint TerritoryId { get; private set; }
    /// <summary>已加载的时间线数据（供 UI 读取元数据）</summary>
    public ExecutionTimelineData? TimelineData { get; private set; }
    /// <summary>调试信息</summary>
    public ExecutionDebug Debug { get; } = new();
    /// <summary>战斗时间（毫秒）</summary>
    public int BattleTimeMs => Context.BattleTimeMs;

    /// <summary>自动攻击开关（由 TriggerActionSwitchPull 控制）</summary>
    public bool IsPullEnabled { get; set; } = true;

    internal Spell? _forceSpell;
    private bool _paused;
    private uint _previousTerritoryId;
    private CancellationTokenSource? _cts;

    /// <summary>WaitCond 注册表</summary>
    private readonly ConcurrentDictionary<TriggerLeafNode, TaskCompletionSource<bool>> _waitingConds = new();

    private ExecutionAxis() { }

    #region 生命周期

    /// <summary>初始化执行轴</summary>
    public void Init()
    {
        if (Initialized) return;
        Initialized = true;
        ExecutionJsonLoader.RegisterBuiltInTypes();
        Events.GameEventHook.Instance.OnEventFired += OnEventFired;
        AutoLoadTimeline();
    }

    /// <summary>关闭执行轴</summary>
    public void Shutdown()
    {
        Events.GameEventHook.Instance.OnEventFired -= OnEventFired;
        Stop();
        Root = null;
        TimelineData = null;
        Initialized = false;
        TimelineName = "";
        TerritoryId = 0;
        _waitingConds.Clear();
    }

    private void OnEventFired(ITriggerCondParams condParams)
    {
        UseCondParams(condParams);
    }

    /// <summary>战斗开始 — 启动触发树（async void）</summary>
    public void Start()
    {
        if (Root == null || IsRunning) return;

        IsRunning = true;
        Hi.Verbose($"[ExecAxis] Start: {TimelineName}");
        Context.Reset();
        Context.WaitCondFn = WaitCond;
        _forceSpell = null;
        _paused = false;
        _waitingConds.Clear();
        _cts = new CancellationTokenSource();

        _ = RunTreeAsync(_cts.Token);
    }

    /// <summary>脱战 — 取消触发树</summary>
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Context.IsDisposed = true;
        _cts?.Cancel();

        // 取消所有等待中的条件
        foreach (var (node, tcs) in _waitingConds)
        {
            Hi.Verbose($"[ExecAxis] Stop: 取消 WaitCond {node.DisplayName}(#{node.Id})");
            tcs.TrySetResult(false);
        }
        _waitingConds.Clear();
    }

    private async Task RunTreeAsync(CancellationToken ct)
    {
        try
        {
            DService.Instance().Log.Debug($"[ExecAxis] 触发树开始: {TimelineName}");
            await Root!.Execute(Context);
            DService.Instance().Log.Debug($"[ExecAxis] 触发树结束: {TimelineName}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ExecAxis] 触发树异常: {ex}");
        }
    }

    #endregion

    #region WaitCond — 事件驱动的条件等待

    /// <summary>
    /// 条件节点挂起等待。注册 TCS，由 CheckWaitingConds 或 UseCondParams 唤醒。
    /// </summary>
    internal Task<bool> WaitCond(TriggerLeafNode node)
    {
        var tcs = new TaskCompletionSource<bool>();
        _waitingConds[node] = tcs;
        Hi.Verbose($"[ExecAxis] WaitCond 注册: {node.DisplayName}(#{node.Id})");
        return tcs.Task;
    }

    /// <summary>每帧检查所有挂起条件</summary>
    private void CheckWaitingConds()
    {
        if (_waitingConds.IsEmpty) return;
        Hi.Verbose($"[ExecAxis] CheckWaitingConds: 检查 {_waitingConds.Count} 个挂起条件");

        var toWake = new List<TriggerLeafNode>();
        foreach (var (node, _) in _waitingConds)
        {
            try
            {
                bool met = node switch
                {
                    TreeCondNode condNode => condNode.EvaluateConds(),
                    TreeScriptNode scriptNode => scriptNode.EvaluateConds(),
                    _ => false
                };
                if (met)
                {
                    Hi.Verbose($"[ExecAxis] CheckWaitingConds: 条件满足 → {node.DisplayName}(#{node.Id})");
                    toWake.Add(node);
                }
            }
            catch { }
        }

        foreach (var node in toWake)
        {
            if (_waitingConds.TryRemove(node, out var tcs))
            {
                Hi.Verbose($"[ExecAxis] WaitCond 唤醒: {node.DisplayName}(#{node.Id})");
                tcs.TrySetResult(true);
            }
        }
    }

    /// <summary>注入触发条件参数，唤醒匹配的条件节点</summary>
    public void UseCondParams(ITriggerCondParams condParams)
    {
        if (_waitingConds.IsEmpty) return;
        Hi.Verbose($"[ExecAxis] UseCondParams: {condParams.GetType().Name} (挂起 {_waitingConds.Count} 个)");

        var toWake = new List<TriggerLeafNode>();
        foreach (var (node, _) in _waitingConds)
        {
            try
            {
                bool met = node switch
                {
                    TreeCondNode condNode => condNode.EvaluateForEvent(condParams),
                    TreeScriptNode scriptNode => scriptNode.EvaluateForEvent(condParams),
                    _ => false
                };
                if (met)
                {
                    Hi.Verbose($"[ExecAxis] UseCondParams 匹配 → {node.DisplayName}(#{node.Id})");
                    toWake.Add(node);
                }
            }
            catch { }
        }

        foreach (var node in toWake)
        {
            if (_waitingConds.TryRemove(node, out var tcs))
                tcs.TrySetResult(true);
        }
    }

    #endregion

    #region 加载

    /// <summary>从 JSON 加载触发树</summary>
    public bool LoadFromJson(string json)
    {
        var data = ExecutionJsonLoader.FromJson(json);
        if (data == null) return false;

        Root = data.Root;
        TimelineName = data.Name;
        TerritoryId = data.TerritoryId;
        TimelineData = data;

        foreach (var kv in data.ExposedVars)
            Context.Variables[kv.Key] = kv.Value;

        DService.Instance().Log.Information($"[ExecAxis] 已加载触发树: {data.Name} (副本 {data.TerritoryId})");
        return true;
    }

    /// <summary>从文件加载触发树</summary>
    public bool LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        return LoadFromJson(File.ReadAllText(filePath));
    }

    /// <summary>是否允许自动加载</summary>
    public bool AutoLoadEnabled { get; set; } = true;

    /// <summary>自动加载当前副本触发树</summary>
    public void AutoLoadTimeline()
    {
        if (!AutoLoadEnabled) return;
        var territoryId = OmenTools.OmenService.GameState.TerritoryType;
        if (territoryId == 0) return;
        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "ExecutionTimelines");
        var filePath = Path.Combine(dir, $"{territoryId}.json");
        if (File.Exists(filePath)) LoadFromFile(filePath);
    }

    #endregion

    #region 每帧

    /// <summary>每帧：检查副本切换 + 唤醒挂起条件 + 输出控制信号</summary>
    public ExecutionOutput? Update(int battleTimeMs)
    {
        Context.BattleTimeMs = battleTimeMs;
        CurrentOutput.Clear();

        if (!Initialized || Root == null) return null;

        // 副本切换 → 重新加载
        var territory = OmenTools.OmenService.GameState.TerritoryType;
        if (territory != 0 && territory != _previousTerritoryId)
        {
            Hi.Verbose($"[ExecAxis] Update: 副本切换 {_previousTerritoryId} → {territory}，重新加载");
            _previousTerritoryId = territory;
            Stop();
            AutoLoadTimeline();
        }

        // 唤醒挂起的条件节点
        Hi.Verbose($"[ExecAxis] Update: battleTimeMs={battleTimeMs}");
        CheckWaitingConds();

        // 检查强制技能
        if (_forceSpell != null)
        {
            Hi.Verbose($"[ExecAxis] Update: 强制技能 {_forceSpell.Name}");
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.ForceSpell = _forceSpell;
            CurrentOutput.Description = $"执行轴强制技能: {_forceSpell.Name}";
            _forceSpell = null;
            return CurrentOutput;
        }

        // 检查暂停
        if (_paused)
        {
            Hi.Verbose($"[ExecAxis] Update: 暂停 ACR");
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.PauseAcr = true;
            CurrentOutput.Description = "执行轴暂停 ACR";
            return CurrentOutput;
        }

        return null;
    }

    #endregion

    internal void SetForceSpell(Spell spell) => _forceSpell = spell;

    internal void SetPause(bool paused)
    {
        _paused = paused;
        if (!paused) CurrentOutput.ResumeAcr = true;
    }

    /// <summary>是否已暂停</summary>
    public bool IsPaused => _paused;
}

/// <summary>执行轴输出数据</summary>
public sealed class ExecutionOutput
{
    /// <summary>是否消费本帧（跳过 ACR 正常循环）</summary>
    public bool ConsumeFrame { get; set; }
    /// <summary>强制释放的技能</summary>
    public Spell? ForceSpell { get; set; }
    /// <summary>强制切换的目标</summary>
    public IBattleChara? ForceTarget { get; set; }
    /// <summary>暂停 ACR</summary>
    public bool PauseAcr { get; set; }
    /// <summary>恢复 ACR</summary>
    public bool ResumeAcr { get; set; }
    /// <summary>输出描述</summary>
    public string Description { get; set; } = "";

    /// <summary>清空输出数据</summary>
    public void Clear()
    {
        ConsumeFrame = false;
        ForceSpell = null;
        ForceTarget = null;
        PauseAcr = false;
        ResumeAcr = false;
        Description = "";
    }
}

/// <summary>执行轴调试信息</summary>
public sealed class ExecutionDebug
{
    /// <summary>战斗时间（毫秒）</summary>
    public int BattleTimeMs { get; set; }
    /// <summary>ACR 暂停状态</summary>
    public bool PauseAcr { get; set; }
    /// <summary>触发器执行历史</summary>
    public List<string> TriggerHistory { get; } = [];

    /// <summary>记录触发器执行</summary>
    public void LogTrigger(string msg)
    {
        if (TriggerHistory.Count >= 20) TriggerHistory.RemoveAt(0);
        TriggerHistory.Add(msg);
    }

    /// <summary>重置调试信息</summary>
    public void Reset()
    {
        BattleTimeMs = 0;
        PauseAcr = false;
        TriggerHistory.Clear();
    }
}
