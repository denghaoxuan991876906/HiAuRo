using HiAuRo.Runtime;

namespace HiAuRo.ACR;

/// <summary>
/// 起手爆发接口 —— 继承 ISlotSequence
/// </summary>
public interface IOpener : ISlotSequence
{
    uint Level { get; }

    /// <summary>倒计时阶段行为注册（AIRunner.Load 时自动调用）</summary>
    void InitCountDown(CountDownHandler handler);
}
