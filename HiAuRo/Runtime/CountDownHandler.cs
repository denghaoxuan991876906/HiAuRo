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

    /// <summary>是否已触发过（防止同一行为重复执行）</summary>
    private readonly HashSet<(int, uint)> _fired = [];

    /// <summary>倒计时是否曾经活跃（用于区分"从未有过倒计时"和"倒计时刚结束"）</summary>
    private bool _countdownWasActive;

    /// <summary>上一帧倒计时是否活跃（用于检测倒计时开始事件）</summary>
    public bool WasActive => _countdownWasActive;

    /// <summary>倒计时是否已结束</summary>
    public bool CountdownFinished { get; private set; }

    /// <summary>注入 SlotExecutor，使预注册行为通过统一路径执行</summary>
    public void SetExecutor(SlotExecutor executor)
    {
        _executor = executor;
        Hi.Debug("[CountDown] SlotExecutor 已注入");
    }

    /// <summary>注册倒计时阶段行为（timeLeftMs 单位毫秒）</summary>
    public void AddAction(int timeLeftMs, uint spellId, SpellTargetType targetType)
    {
        Hi.Debug($"[CountDown] 注册预动作: spell={spellId} target={targetType} timeLeftMs={timeLeftMs}");
        _actions.Add((timeLeftMs, spellId, targetType));
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

            foreach (var (timeLeftMs, spellId, targetType) in _actions)
            {
                if (Math.Abs(countdownMs - timeLeftMs) <= 500 && _fired.Add((timeLeftMs, spellId)))
                {
                    Hi.Debug($"[CountDown] 触发预动作: spell={spellId} target={targetType} timeLeftMs={timeLeftMs} countdownMs={countdownMs:F0}");
                    var spell = new Spell(spellId, targetType);
                    var slot = new Slot(spell);
                    _executor?.StartSlot(slot);
                }
            }
        }
        else if (_countdownWasActive && !CountdownFinished)
        {
            CountdownFinished = true;
            Hi.Debug("[CountDown] 倒计时结束, CountdownFinished=true");
        }
    }

    /// <summary>是否还有未执行的行为</summary>
    public bool HasPending => _actions.Count > 0;

    /// <summary>重置倒计时</summary>
    public void Reset()
    {
        _actions.Clear();
        _fired.Clear();
        _countdownWasActive = false;
        CountdownFinished = false;
    }
}
