using OmenTools.OmenService;

namespace HiAuRo;

public static partial class Data
{
/// <summary>
/// 目标数据 —— 转发 TargetManager.*
/// </summary>
public static class Target
{
    public static IGameObject? Current => TargetManager.Target;

    public static IGameObject? Focus => TargetManager.FocusTarget;

    public static IGameObject? MouseOver => TargetManager.MouseOverTarget;

    public static IGameObject? Soft => TargetManager.SoftTarget;

    public static IGameObject? Previous => TargetManager.PreviousTarget;
}
}
