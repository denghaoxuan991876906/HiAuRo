using HiAuRo.FactAxis;

namespace HiAuRo;

/// <summary>
/// HiAuRo 游戏数据统一入口
/// </summary>
public static partial class Data
{
    public static bool IsReady =>
        OmenTools.OmenService.GameState.IsLoggedIn &&
        OmenTools.OmenService.GameState.IsTerritoryLoaded;

    /// <summary>事实轴当前运行时状态快照。FactAxis 未运行时返回 null。</summary>
    public static FactAxis.FactState? FactState =>
        FactTimeline.Instance is { IsRunning: true } ? FactTimeline.Instance.State : null;
}
