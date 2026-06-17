using System.Collections.Concurrent;
using HiAuRo.ACR;

namespace HiAuRo.Execution;

/// <summary>
/// 辅助轴 — 独立于执行轴/事实轴的并行触发树
/// 复用 ExecutionNode AST + ScriptCompiler，从 .txt 加载
/// </summary>
public sealed class AssistAxis
{
    public static AssistAxis Instance { get; } = new();

    public TriggerCompositeNode? Root { get; private set; }
    public EvalContext Context { get; } = new();
    public ExecutionOutput CurrentOutput { get; } = new();
    public bool IsRunning { get; private set; }
    public bool Initialized { get; private set; }
    public string TimelineName { get; private set; } = "";

    internal Spell? _forceSpell;
    private bool _paused;
    private bool _autoLoadDisabled;

    /// <summary>是否允许自动加载</summary>
    public bool AutoLoadEnabled
    {
        get => !_autoLoadDisabled;
        set => _autoLoadDisabled = !value;
    }
    private uint _previousTerritoryId;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<TriggerLeafNode, TaskCompletionSource<bool>> _waitingConds = new();

    private AssistAxis() { }

    public void Init()
    {
        if (Initialized) return;
        Initialized = true;
        AutoLoadTimeline();
    }

    public void Shutdown()
    {
        Stop();
        Root = null;
        Initialized = false;
        TimelineName = "";
        _waitingConds.Clear();
    }

    public void Start()
    {
        if (Root == null || IsRunning) return;
        IsRunning = true;
        Context.Reset();
        Context.WaitCondFn = WaitCond;
        _forceSpell = null;
        _paused = false;
        _waitingConds.Clear();
        _cts = new CancellationTokenSource();
        Hi.Verbose($"[AssistAxis] Start: {TimelineName}");
        _ = RunTreeAsync(_cts.Token);
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Context.IsDisposed = true;
        _cts?.Cancel();
        foreach (var (node, tcs) in _waitingConds)
        {
            Hi.Verbose($"[AssistAxis] Stop: 取消 WaitCond {node.DisplayName}(#{node.Id})");
            tcs.TrySetResult(false);
        }
        _waitingConds.Clear();
    }

    private async Task RunTreeAsync(CancellationToken ct)
    {
        try
        {
            DService.Instance().Log.Debug($"[AssistAxis] 辅助树开始: {TimelineName}");
            await Root!.Execute(Context);
            DService.Instance().Log.Debug($"[AssistAxis] 辅助树结束: {TimelineName}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { DService.Instance().Log.Error($"[AssistAxis] 辅助树异常: {ex}"); }
    }

    internal Task<bool> WaitCond(TriggerLeafNode node)
    {
        var tcs = new TaskCompletionSource<bool>();
        _waitingConds[node] = tcs;
        Hi.Verbose($"[AssistAxis] WaitCond 注册: {node.DisplayName}(#{node.Id})");
        return tcs.Task;
    }

    private void CheckWaitingConds()
    {
        if (_waitingConds.IsEmpty) return;
        Hi.Verbose($"[AssistAxis] CheckWaitingConds: 检查 {_waitingConds.Count} 个挂起条件");
        foreach (var (node, tcs) in _waitingConds)
        {
            try
            {
                bool met = node switch
                {
                    TreeCondNode c => c.EvaluateConds(),
                    TreeScriptNode s => s.EvaluateConds(),
                    _ => false
                };
                if (met && _waitingConds.TryRemove(node, out var removedTcs))
                {
                    Hi.Verbose($"[AssistAxis] WaitCond 唤醒: {node.DisplayName}(#{node.Id})");
                    removedTcs.TrySetResult(true);
                }
            }
            catch { }
        }
    }

    /// <summary>注入触发条件参数，唤醒匹配的条件节点</summary>
    internal void UseCondParams(ITriggerCondParams condParams)
    {
        if (_waitingConds.IsEmpty) return;
        Hi.Verbose($"[AssistAxis] UseCondParams: {condParams.GetType().Name} (挂起 {_waitingConds.Count} 个)");

        var toWake = new List<TriggerLeafNode>();
        foreach (var (node, _) in _waitingConds)
        {
            try
            {
                bool met = node switch
                {
                    TreeCondNode c => c.EvaluateForEvent(condParams),
                    TreeScriptNode s => s.EvaluateForEvent(condParams),
                    _ => false
                };
                if (met) toWake.Add(node);
            }
            catch { }
        }

        foreach (var node in toWake)
        {
            if (_waitingConds.TryRemove(node, out var tcs))
                tcs.TrySetResult(true);
        }
    }

    #region 加载 (.txt)

    public bool LoadFromJson(string json)
    {
        var data = ExecutionJsonLoader.FromJson(json);
        if (data == null) return false;
        Root = data.Root;
        TimelineName = data.Name;
        foreach (var kv in data.ExposedVars)
            Context.Variables[kv.Key] = kv.Value;
        DService.Instance().Log.Information($"[AssistAxis] 已加载: {data.Name}");
        return true;
    }

    public bool LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        return LoadFromJson(File.ReadAllText(filePath));
    }

    public void AutoLoadTimeline()
    {
        if (_autoLoadDisabled) return;
        var territoryId = OmenTools.OmenService.GameState.TerritoryType;
        if (territoryId == 0) return;
        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "AssistTimelines");
        var filePath = Path.Combine(dir, $"{territoryId}.txt");
        if (File.Exists(filePath)) LoadFromFile(filePath);
    }

    public void LoadAssistTimeline()
    {
        _autoLoadDisabled = false;
        AutoLoadTimeline();
    }

    public void UnloadAssistTimeline()
    {
        _autoLoadDisabled = true;
        Stop();
        Root = null;
        TimelineName = "";
    }

    #endregion

    #region 每帧

    public ExecutionOutput? Update(int battleTimeMs)
    {
        Context.BattleTimeMs = battleTimeMs;
        CurrentOutput.Clear();

        if (!Initialized || Root == null) return null;

        var territory = OmenTools.OmenService.GameState.TerritoryType;
        if (territory != 0 && territory != _previousTerritoryId)
        {
            Hi.Verbose($"[AssistAxis] Update: 副本切换 {_previousTerritoryId} → {territory}，重新加载");
            _previousTerritoryId = territory;
            Stop();
            AutoLoadTimeline();
        }

        Hi.Verbose($"[AssistAxis] Update: battleTimeMs={battleTimeMs}");
        CheckWaitingConds();

        if (_forceSpell != null)
        {
            Hi.Verbose($"[AssistAxis] Update: 强制技能 {_forceSpell.Name}");
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.ForceSpell = _forceSpell;
            CurrentOutput.Description = $"辅助轴强制技能: {_forceSpell.Name}";
            _forceSpell = null;
            return CurrentOutput;
        }

        if (_paused)
        {
            Hi.Verbose($"[AssistAxis] Update: 暂停 ACR");
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.PauseAcr = true;
            CurrentOutput.Description = "辅助轴暂停 ACR";
            return CurrentOutput;
        }

        return null;
    }

    #endregion

    internal void SetForceSpell(Spell spell) => _forceSpell = spell;
    internal void SetPause(bool paused) { _paused = paused; if (!paused) CurrentOutput.ResumeAcr = true; }
    public bool IsPaused => _paused;
}
