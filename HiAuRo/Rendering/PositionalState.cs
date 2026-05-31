using OmenTools.OmenService;

namespace HiAuRo.Rendering;

/// <summary>身位方向枚举</summary>
public enum PositionalDir { None, Behind, Flank }

/// <summary>
/// 身位状态 — ACR 推送身位需求，每帧更新进度和正确性
/// </summary>
public static class PositionalState
{
    public static PositionalDir ActiveDir { get; private set; }
    public static float Progress { get; private set; }      // 0-1 进度
    public static float RemainingMs { get; private set; }
    public static uint ActionId { get; private set; }
    public static bool IsCorrectPosition { get; private set; }

    static float _totalMs;

    /// <summary>ACR 推送身位需求</summary>
    public static void Push(PositionalDir dir, int timeMs, uint actionId = 0)
    {
        ActiveDir = dir;
        RemainingMs = timeMs;
        _totalMs = timeMs;
        ActionId = actionId;
        Progress = 0f;
    }

    /// <summary>每帧更新进度和位置正确性</summary>
    public static void Update(float dtSeconds)
    {
        if (ActiveDir == PositionalDir.None) return;

        RemainingMs -= dtSeconds * 1000f;
        if (RemainingMs <= 0)
        {
            RemainingMs = 0;
            Progress = 1f;
            return;
        }
        Progress = 1f - (RemainingMs / _totalMs);

        var target = TargetManager.Target;
        if (Data.Me.Object != null && target != null)
        {
            IsCorrectPosition = ActiveDir switch
            {
                PositionalDir.Behind => ACR.TargetHelper.IsBehind(target),
                PositionalDir.Flank => ACR.TargetHelper.IsFlanking(target),
                _ => false
            };
        }
        else
        {
            IsCorrectPosition = false;
        }
    }

    /// <summary>清除当前身位状态</summary>
    public static void Clear()
    {
        ActiveDir = PositionalDir.None;
        Progress = 0f;
        RemainingMs = 0f;
        _totalMs = 0f;
        ActionId = 0;
        IsCorrectPosition = false;
    }
}
