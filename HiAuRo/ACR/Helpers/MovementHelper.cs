using System.Numerics;
using OmenTools;
using OmenTools.Interop.Game.Models;
using OmenTools.Interop.Game.Models.Packets.Downstream;

namespace HiAuRo.ACR;

/// <summary>
/// 移动/TP 快捷操作 —— ACR 在事件回调中直接调用，立即执行
/// </summary>
public static class MovementHelper
{
    private static unsafe ActorSetPosPacket.Delegate? _tpFunc;
    private static readonly object _lock = new();

    /// <summary>寻路移动到目标位置（依赖 VNavmesh IPC）</summary>
    public static void MoveTo(Vector3 target)
    {
        try
        {
            DService.Instance().PI
                .GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo")
                .InvokeFunc(target, false);
        }
        catch { /* VNavmesh 未安装 */ }
    }

    /// <summary>瞬移到目标位置（内部实现，通过 ActorSetPos 封包）</summary>
    public static unsafe void TeleportTo(Vector3 target)
    {
        var localPlayer = DService.Instance().ObjectTable.LocalPlayer;
        if (localPlayer == null) return;

        if (_tpFunc == null)
        {
            lock (_lock)
            {
                _tpFunc ??= ActorSetPosPacket.Signature.GetDelegate<ActorSetPosPacket.Delegate>();
            }
        }
        if (_tpFunc == null) return;

        var packet = new ActorSetPosPacket(target);
        _tpFunc(localPlayer.EntityID, &packet);
    }

    /// <summary>停住移动</summary>
    public static void Stop()
    {
        try
        {
            DService.Instance().PI
                .GetIpcSubscriber<object>("vnavmesh.Path.Stop")
                .InvokeAction();
        }
        catch { /* VNavmesh 未安装 */ }
    }
}
