using HiAuRo.ACR;
using HiAuRo.Infrastructure;

namespace HiAuRo.Runtime;

/// <summary>
/// 倒计时管理器 —— 管理副本倒计时阶段的预注册行为（时间单位：毫秒，与 AE 一致）
/// </summary>
public sealed class CountDownHandler
{
    private SlotExecutor? _executor;

    /// <summary>已注册的倒计时行为（timeLeft 单位为毫秒）</summary>
    private readonly List<(int TimeLeftMs, uint SpellId, SpellTargetType TargetType)> _actions = [];

    /// <summary>已注册的动态倒计时行为（timeLeft 单位为毫秒）</summary>
    private readonly List<CountDownAction> _callbackActions = [];

    private readonly Queue<Slot> _pendingSlots = [];

    /// <summary>是否已触发过（防止同一行为重复执行）</summary>
    private readonly HashSet<(int, uint)> _fired = [];

    /// <summary>倒计时是否曾经活跃（用于区分"从未有过倒计时"和"倒计时刚结束"）</summary>
    private bool _countdownWasActive;

    private float? _lastCountdownMs;

    /// <summary>上一帧倒计时是否活跃（用于检测倒计时开始事件）</summary>
    public bool WasActive => _countdownWasActive;

    /// <summary>倒计时是否已结束</summary>
    public bool CountdownFinished { get; private set; }

    /// <summary>注入 SlotExecutor，使预注册行为通过统一路径执行</summary>
    public void SetExecutor(SlotExecutor executor)
    {
        _executor = executor;
        Debug("[CountDown] SlotExecutor 已注入");
    }

    /// <summary>注册倒计时阶段行为（timeLeftMs 单位毫秒）</summary>
    public void AddAction(int timeLeftMs, uint spellId, SpellTargetType targetType)
    {
        Debug($"[CountDown] 注册预动作: spell={spellId} target={targetType} timeLeftMs={timeLeftMs}");
        _actions.Add((timeLeftMs, spellId, targetType));
    }

    /// <summary>注册动态倒计时阶段行为（timeLeftMs 单位毫秒）</summary>
    public void AddAction(int timeLeftMs, Func<Spell?> createSpell)
    {
        Debug($"[CountDown] 注册动态预动作: timeLeftMs={timeLeftMs}");
        _callbackActions.Add(new CountDownAction(timeLeftMs, createSpell));
    }

    /// <summary>
    /// 每帧推进 —— 检查倒计时剩余时间
    /// </summary>
    /// <param name="countdownMs">当前倒计时剩余毫秒数（0 表示倒计时未开始或已结束）</param>
    public void Update(float countdownMs)
    {
        if (countdownMs > 0)
        {
            _countdownWasActive = true;
            TryStartPending();

            foreach (var (timeLeftMs, spellId, targetType) in _actions)
            {
                if (IsDue(timeLeftMs, countdownMs, _lastCountdownMs) && !_fired.Contains((timeLeftMs, spellId)))
                {
                    Debug($"[CountDown] 触发预动作: spell={spellId} target={targetType} timeLeftMs={timeLeftMs} countdownMs={countdownMs:F0}");
                    var spell = new Spell(spellId, targetType);
                    var slot = new Slot(spell);
                    EnqueueOrStart(slot);
                    _fired.Add((timeLeftMs, spellId));
                }
            }

            foreach (var action in _callbackActions)
            {
                var slot = CountDownActionRunner.TryBuildSlot(action, countdownMs, _lastCountdownMs, markFired: false);
                if (slot != null)
                {
                    EnqueueOrStart(slot);
                    action.MarkFired();
                }
            }

            TryStartPending();
            _lastCountdownMs = countdownMs;
        }
        else if (_countdownWasActive && !CountdownFinished)
        {
            CountdownFinished = true;
            Debug("[CountDown] 倒计时结束, CountdownFinished=true");
        }
    }

    /// <summary>是否还有未执行的行为</summary>
    public bool HasPending => _actions.Count > 0 || _callbackActions.Count > 0;

    /// <summary>重置倒计时</summary>
    public void Reset()
    {
        _actions.Clear();
        _callbackActions.Clear();
        _pendingSlots.Clear();
        _fired.Clear();
        _countdownWasActive = false;
        _lastCountdownMs = null;
        CountdownFinished = false;
    }

    internal static bool IsDue(int timeLeftMs, float countdownMs, float? lastCountdownMs)
    {
        if (lastCountdownMs == null)
            return Math.Abs(countdownMs - timeLeftMs) <= 500;

        return lastCountdownMs > timeLeftMs && countdownMs <= timeLeftMs;
    }

    private void EnqueueOrStart(Slot slot)
    {
        if (_executor == null)
        {
            PrioritySlotStack.Instance.Push(PrioritySlotStack.Priority.Opener, slot);
            return;
        }

        if (_executor.IsExecuting || _pendingSlots.Count > 0)
        {
            _pendingSlots.Enqueue(slot);
            return;
        }

        _executor.StartSlot(slot);
    }

    private void TryStartPending()
    {
        if (_executor == null || _executor.IsExecuting || _pendingSlots.Count == 0)
            return;

        _executor.StartSlot(_pendingSlots.Dequeue());
    }

    private static void Debug(string message)
    {
        try
        {
            Hi.Debug(message);
        }
        catch
        {
            // 离线测试环境没有 Dalamud 日志服务，倒计时逻辑不应因此中断。
        }
    }
}

/// <summary>动态倒计时动作，触发时再生成技能。</summary>
public sealed class CountDownAction
{
    public CountDownAction(int timeLeftMs, Func<Spell?> createSpell)
    {
        TimeLeftMs = timeLeftMs;
        CreateSpell = createSpell ?? throw new ArgumentNullException(nameof(createSpell));
    }

    public int TimeLeftMs { get; }
    public Func<Spell?> CreateSpell { get; }
    public bool Fired { get; private set; }

    internal bool IsDue(float countdownMs, float? lastCountdownMs) =>
        !Fired && CountDownHandler.IsDue(TimeLeftMs, countdownMs, lastCountdownMs);

    internal void MarkFired() => Fired = true;
}

public static class CountDownActionRunner
{
    public static Slot? TryBuildSlot(CountDownAction action, float countdownMs, float? lastCountdownMs = null, bool markFired = true)
    {
        if (!action.IsDue(countdownMs, lastCountdownMs))
            return null;

        var spell = action.CreateSpell();
        if (spell == null)
            return null;

        if (markFired)
            action.MarkFired();
        return new Slot(spell);
    }
}
