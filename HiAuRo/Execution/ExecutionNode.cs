using HiAuRo.ACR;

namespace HiAuRo.Execution;

/// <summary>
/// 触发树节点基类
/// 异步一次性求值
/// </summary>
public abstract class TriggerNode
{
    /// <summary>节点 ID</summary>
    public int Id { get; set; }
    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>备注</summary>
    public string Remark { get; set; } = "";
    /// <summary>标签</summary>
    public string Tag { get; set; } = "";
    /// <summary>是否启用</summary>
    public bool Enable { get; set; } = true;

    /// <summary>异步求值。Task 完成 = 节点终结</summary>
    public async Task<bool> Execute(EvalContext ctx)
    {
        if (!Enable) return true;
        if (ctx.IsDisposed) return false;
        Hi.Verbose($"[ExecNode] ▶ {DisplayName}(#{Id}) 进入");
        var result = await OnExecute(ctx);
        Hi.Verbose($"[ExecNode] ◀ {DisplayName}(#{Id}) 退出 → {(result ? "成功" : "失败")}");
        return result;
    }

    /// <summary>子类实现的异步求值逻辑</summary>
    protected abstract Task<bool> OnExecute(EvalContext ctx);
}

/// <summary>组合节点</summary>
public abstract class TriggerCompositeNode : TriggerNode
{
    /// <summary>子节点列表</summary>
    public List<TriggerNode> Childs { get; set; } = [];
}

/// <summary>叶子节点</summary>
public abstract class TriggerLeafNode : TriggerNode { }

#region 组合节点

/// <summary>
/// 序列节点 — 依次 await 子节点
/// IgnoreNodeResult: 忽略子节点失败，总是继续
/// </summary>
public sealed class TreeSequence : TriggerCompositeNode
{
    /// <summary>是否忽略子节点失败结果</summary>
    public bool IgnoreNodeResult { get; set; }

    /// <summary>依次执行子节点，支持短路</summary>
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        for (int i = 0; i < Childs.Count; i++)
        {
            var child = Childs[i];
            if (!child.Enable) continue;
            if (ctx.IsDisposed) return false;
            Hi.Verbose($"[ExecNode] Sequence({Tag}) → 子节点 [{i}/{Childs.Count}] {child.DisplayName}(#{child.Id})");
            var result = await child.Execute(ctx);
            if (!result && !IgnoreNodeResult)
            {
                Hi.Verbose($"[ExecNode] Sequence({Tag}) 短路退出");
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// 并行节点
/// AnyReturn: 竞赛模式，最先完成的胜出
/// </summary>
public sealed class TreeParallel : TriggerCompositeNode
{
    /// <summary>竞赛模式：最先完成的胜出</summary>
    public bool AnyReturn { get; set; }

    /// <summary>并行执行所有子节点</summary>
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        if (AnyReturn)
        {
            var branches = Childs.Where(c => c.Enable)
                .Select(c => (Node: c, Context: ctx.CreateChild()))
                .ToList();
            var tasks = branches.Select(b => b.Node.Execute(b.Context)).ToList();

            Hi.Verbose($"[ExecNode] Parallel({Tag}) 启动 {tasks.Count} 子节点 [AnyReturn={AnyReturn}]");
            if (tasks.Count == 0) return true;

            var winner = await Task.WhenAny(tasks);
            foreach (var branch in branches)
                branch.Context.IsDisposed = true;
            Hi.Verbose($"[ExecNode] Parallel({Tag}) 竞赛胜出");
            return await winner;
        }

        var allTasks = Childs.Where(c => c.Enable).Select(c => c.Execute(ctx)).ToList();
        Hi.Verbose($"[ExecNode] Parallel({Tag}) 启动 {allTasks.Count} 子节点 [AnyReturn={AnyReturn}]");
        if (allTasks.Count == 0) return true;

        await Task.WhenAll(allTasks);
        Hi.Verbose($"[ExecNode] Parallel({Tag}) 全部完成");
        return true;
    }
}

/// <summary>
/// 选择节点 — 依次尝试，第一个成功的返回
/// 所有分支都失败时仍返回 true
/// </summary>
public sealed class TreeSelect : TriggerCompositeNode
{
    /// <summary>依次尝试子节点，第一个成功返回</summary>
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        foreach (var child in Childs)
        {
            if (!child.Enable) continue;
            Hi.Verbose($"[ExecNode] Select({Tag}) → 尝试 {child.DisplayName}(#{child.Id})");
            if (await child.Execute(ctx))
            {
                Hi.Verbose($"[ExecNode] Select({Tag}) → {child.DisplayName} 成功");
                return true;
            }
            Hi.Verbose($"[ExecNode] Select({Tag}) → {child.DisplayName} 失败");
        }
        return true;
    }
}

/// <summary>
/// 循环节点 — 重复 Times 次
/// </summary>
public sealed class TreeLoop : TriggerCompositeNode
{
    /// <summary>循环次数</summary>
    public int Times { get; set; } = 1;

    /// <summary>重复执行子节点 Times 次</summary>
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        for (int i = 0; i < Times; i++)
        {
            Hi.Verbose($"[ExecNode] Loop({Tag}) 第 {i + 1}/{Times} 轮");
            foreach (var child in Childs)
            {
                if (!child.Enable) continue;
                if (ctx.IsDisposed) return false;
                await child.Execute(ctx);
            }
        }
        return true;
    }
}

/// <summary>
/// 脚本节点 — C# 动态编译执行
/// Check() 返回 false = 继续等待，true = 节点完成
/// </summary>
public sealed class TreeScriptNode : TriggerLeafNode
{
    /// <summary>是否仅检查（不等待）</summary>
    public bool OnlyCheck { get; set; }
    /// <summary>脚本内容</summary>
    public string Script { get; set; } = "";

    /// <summary>
    /// 绑定的事实轴节点 ID，脚本产生的 MovementDemand 自动继承
    /// </summary>
    public string FactNodeId { get; set; } = "";

    private ITriggerScript? _compiled;

    /// <summary>编译并执行脚本，OnlyCheck 时立即返回，否则进入等待</summary>
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        if (string.IsNullOrWhiteSpace(Script)) return true;
        _compiled ??= ScriptCompiler.Compile(Script);
        if (_compiled == null) return true;

        if (OnlyCheck)
        {
            try
            {
                var checkResult = _compiled.Check(null);
                Hi.Verbose($"[ExecNode] Script({Tag}) OnlyCheck: {checkResult}");
                return checkResult;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[TreeScriptNode] 脚本执行异常: {ex.Message}");
                return false;
            }
        }

        try { _compiled.Check(null); }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[TreeScriptNode] 脚本执行异常: {ex.Message}");
        }
        Hi.Verbose($"[ExecNode] Script({Tag}) 进入等待模式");
        var result = await (ctx.WaitCondFn?.Invoke(this) ?? Task.FromResult(false));
        Hi.Verbose($"[ExecNode] Script({Tag}) WaitCond 返回: {(result ? "满足" : "取消")}");
        return result;
    }

    /// <summary>每帧轮询：Check() 返回 false = 继续等，true = 节点完成</summary>
    public bool EvaluateConds()
    {
        if (_compiled == null) return true;
        try
        {
            var r = _compiled.Check(null);
            Hi.Verbose($"[ExecNode] Script({Tag}) EvaluateConds: {r}");
            return r;
        }
        catch { return false; }
    }

    /// <summary>事件驱动：传入 ITriggerCondParams，返回 false = 继续等，true = 节点完成</summary>
    public bool EvaluateForEvent(ITriggerCondParams condParams)
    {
        if (_compiled == null) return true;
        try { return _compiled.Check(condParams); }
        catch { return false; }
    }
}

#endregion

#region 叶子节点

/// <summary>条件逻辑类型</summary>
public enum CondLogicType
{
    /// <summary>与（所有条件都必须满足）</summary>
    And = 0,
    /// <summary>或（任一条件满足即可）</summary>
    Or = 1
}

/// <summary>
/// 条件节点
/// CheckOnce: 立即检查
/// 等待模式: 注册 WaitCond
/// </summary>
public sealed class TreeCondNode : TriggerLeafNode
{
    /// <summary>触发条件列表</summary>
    public List<ITriggerCond> TriggerConds { get; set; } = [];
    /// <summary>条件逻辑类型（And/Or）</summary>
    public CondLogicType CondLogicType { get; set; } = CondLogicType.And;
    /// <summary>是否只检查一次</summary>
    public bool CheckOnce { get; set; }
    /// <summary>是否反转条件结果</summary>
    public bool ReverseResult { get; set; }

    /// <summary>CheckOnce 立即检查，否则等待条件满足</summary>
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        if (CheckOnce)
        {
            var met = EvaluateConds();
            Hi.Verbose($"[ExecNode] Cond({Tag}) CheckOnce: {(met ? "满足" : "不满足")}");
            return met;
        }
        else
        {
            Hi.Verbose($"[ExecNode] Cond({Tag}) 进入等待模式");
            var result = await (ctx.WaitCondFn?.Invoke(this) ?? Task.FromResult(false));
            Hi.Verbose($"[ExecNode] Cond({Tag}) WaitCond 返回: {(result ? "满足" : "取消")}");
            return result;
        }
    }

    /// <summary>执行条件检查（供 ExecutionAxis 事件驱动调用）</summary>
    public bool EvaluateConds()
    {
        if (TriggerConds.Count == 0) return true;

        bool met = CondLogicType == CondLogicType.And
            ? TriggerConds.All(c => { try { return c.Handle(); } catch { return false; } })
            : TriggerConds.Any(c => { try { return c.Handle(); } catch { return false; } });

        if (ReverseResult) met = !met;
        Hi.Verbose($"[ExecNode] Cond({Tag}) [{CondLogicType}] {TriggerConds.Count}个条件 → {(met ? "满足" : "不满足")}");
        return met;
    }

    /// <summary>事件驱动求值（带 condParams 上下文）</summary>
    public bool EvaluateForEvent(ITriggerCondParams condParams)
    {
        if (TriggerConds.Count == 0) return true;

        bool met = CondLogicType == CondLogicType.And
            ? TriggerConds.All(c => { try { return c.Handle(condParams); } catch { return false; } })
            : TriggerConds.Any(c => { try { return c.Handle(condParams); } catch { return false; } });

        if (ReverseResult) met = !met;
        return met;
    }
}

/// <summary>动作节点 — 总是成功</summary>
public sealed class TreeActionNode : TriggerLeafNode
{
    /// <summary>触发动作列表</summary>
    public List<ITriggerAction> TriggerActions { get; set; } = [];

    /// <summary>依次执行所有触发动作</summary>
    protected override Task<bool> OnExecute(EvalContext ctx)
    {
        foreach (var action in TriggerActions)
        {
            try
            {
                Hi.Verbose($"[ExecNode] Action({Tag}) → {action.GetType().Name}");
                action.Handle();
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[TriggerNode] 动作异常: {ex.Message}"); }
        }
        return Task.FromResult(true);
    }
}

/// <summary>延迟节点 — await Coroutine.DelayAsync</summary>
public sealed class TreeDelayNode : TriggerLeafNode
{
    /// <summary>延迟秒数</summary>
    public double Delay { get; set; }

    /// <summary>异步等待延迟时间</summary>
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        if (ctx.IsDisposed) return false;
        Hi.Verbose($"[ExecNode] Delay({Tag}) 延迟 {Delay}s 开始");
        await HiAuRo.Runtime.Coroutine.Instance.DelayAsync(Delay * 1000);
        Hi.Verbose($"[ExecNode] Delay({Tag}) 延迟完成");
        return !ctx.IsDisposed;
    }
}

/// <summary>打印调试信息</summary>
public sealed class TreePrintDebugInfoNode : TriggerLeafNode
{
    /// <summary>调试信息内容</summary>
    public string Info { get; set; } = "";
    /// <summary>输出调试日志</summary>
    protected override Task<bool> OnExecute(EvalContext ctx)
    {
        DService.Instance().Log.Information($"[TriggerNode] {Info}");
        return Task.FromResult(true);
    }
}

/// <summary>清除等待条件</summary>
public sealed class TreeClearWaitNode : TriggerLeafNode
{
    /// <summary>是否仅清除前置节点</summary>
    public bool OnlyPreNode { get; set; } = true;
    /// <summary>清除操作（当前为 no-op）</summary>
    protected override Task<bool> OnExecute(EvalContext ctx) => Task.FromResult(true);
}

#endregion

#region 上下文

/// <summary>求值上下文 — 传递执行轴运行时状态</summary>
public sealed class EvalContext
{
    /// <summary>是否已释放</summary>
    public bool IsDisposed { get; set; }
    /// <summary>战斗时间（毫秒）</summary>
    public int BattleTimeMs { get; set; }
    /// <summary>变量字典</summary>
    public Dictionary<string, int> Variables { get; } = [];

    /// <summary>所属轴的 WaitCond 委托（由轴 Start 时注入；节点据此路由到正确的轴，避免跨轴串扰）</summary>
    public Func<TriggerLeafNode, Task<bool>>? WaitCondFn { get; set; }

    public EvalContext CreateChild()
    {
        var child = new EvalContext
        {
            BattleTimeMs = BattleTimeMs,
            WaitCondFn = WaitCondFn
        };

        foreach (var kv in Variables)
            child.Variables[kv.Key] = kv.Value;

        return child;
    }

    /// <summary>获取变量值</summary>
    public int GetVariable(string name) =>
        Variables.TryGetValue(name, out var v) ? v : 0;

    /// <summary>设置变量值</summary>
    public void SetVariable(string name, int value) =>
        Variables[name] = value;

    /// <summary>重置上下文状态</summary>
    public void Reset()
    {
        IsDisposed = false;
        BattleTimeMs = 0;
    }
}

#endregion
