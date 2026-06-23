using System.Numerics;
using OmenTools.Interop.Game.Models;
using OmenTools.Info.Game.Packets.Downstream;

namespace HiAuRo.Runtime.Movement;

public static class MovementDriver
{
    private static unsafe ActorSetPosPacket.Delegate? _tpFunc;
    private static readonly object TpLock = new();
    public static string LastNavMoveError { get; private set; } = "";

    public static bool TryStartNavMeshMove(Vector3 pos)
    {
        try
        {
            LastNavMoveError = "";
            DService.Instance().PI
                .GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo")
                .InvokeFunc(pos, false);
            return true;
        }
        catch (Exception ex)
        {
            LastNavMoveError = ex.Message;
            return false;
        }
    }

    public static void Stop()
    {
        try
        {
            DService.Instance().PI.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
        }
        catch
        {
        }
    }

    public static unsafe bool TryTeleport(Vector3 pos)
    {
        var localPlayer = DService.Instance().ObjectTable.LocalPlayer;
        if (localPlayer == null) return false;

        if (_tpFunc == null)
        {
            lock (TpLock)
            {
                _tpFunc ??= ActorSetPosPacket.Signature.GetDelegate<ActorSetPosPacket.Delegate>();
            }
        }

        if (_tpFunc == null) return false;

        var packet = new ActorSetPosPacket(pos);
        _tpFunc(localPlayer.EntityID, &packet);
        return true;
    }
}
