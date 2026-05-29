using HiAuRo.Infrastructure;

namespace HiAuRo.ACR;

/// <summary>
/// 起手管理器 —— 状态机驱动起手爆发执行
/// 改为 PeekCurrentSlot + Advance 模式，由 SlotExecutor 跨帧执行
/// </summary>
public sealed class OpenerMgr
{
    public enum State
    {
        NotStarted,
        Running,
        Finished
    }

    public State CurrentState { get; private set; } = State.NotStarted;

    private IOpener? _currentOpener;
    private int _currentStep;
    private Slot? _currentSlot;

    /// <summary>开始执行起手</summary>
    public bool Start(IOpener opener)
    {
        if (_currentOpener != null)
            Reset();

        if (opener.StartCheck() < 0)
            return false;

        _currentOpener = opener;
        _currentStep = 0;
        CurrentState = State.Running;

        // 构建第一个 Slot
        BuildCurrentSlot();

        Hi.Debug($"[OpenerMgr] 启动: {opener.GetType().Name}, Steps={opener.Sequence.Count}");
        return true;
    }

    /// <summary>返回当前要执行的 Slot（不推进）。返回 null 表示已完成。</summary>
    public Slot? PeekCurrentSlot()
    {
        if (CurrentState != State.Running || _currentOpener == null)
            return null;

        return _currentSlot;
    }

    /// <summary>当前 Slot 完成后调用，推进到下一个 Step</summary>
    public void Advance()
    {
        if (CurrentState != State.Running || _currentOpener == null)
            return;

        _currentStep++;
        BuildCurrentSlot();

        if (_currentSlot == null)
        {
            // 所有 Step 执行完毕
            CurrentState = State.Finished;
            Hi.Debug("[OpenerMgr] 起手序列完成");
        }
    }

    /// <summary>构建当前 Step 的 Slot</summary>
    private void BuildCurrentSlot()
    {
        if (_currentOpener == null || _currentStep >= _currentOpener.Sequence.Count)
        {
            _currentSlot = null;
            return;
        }

        _currentSlot = new Slot();
        _currentOpener.Sequence[_currentStep](_currentSlot);

        if (_currentSlot.Actions.Count == 0)
        {
            // 空 Slot，跳过
            _currentSlot = null;
            _currentStep++;
            BuildCurrentSlot();
        }
    }

    /// <summary>检查是否可在当前步中断</summary>
    public bool CanInterrupt()
    {
        if (_currentOpener == null || CurrentState != State.Running) return true;
        return _currentOpener.StopCheck(_currentStep) >= 0;
    }

    public void Reset()
    {
        _currentOpener = null;
        _currentStep = 0;
        _currentSlot = null;
        CurrentState = State.NotStarted;
    }
}
