using System.Runtime.InteropServices;

namespace HiAuRo.Execution.Events.Structures;

/// <summary>网络包 — ActorCast 结构体</summary>
[StructLayout(LayoutKind.Explicit, Size = 0x30)]
public struct PacketActorCast
{
    /// <summary>技能 ID</summary>
    [FieldOffset(0x00)] public ushort ActionID;
    /// <summary>读条时间</summary>
    [FieldOffset(0x04)] public float CastTime;
    /// <summary>目标 ID</summary>
    [FieldOffset(0x08)] public uint TargetID;
    /// <summary>朝向</summary>
    [FieldOffset(0x0E)] public float Rotation;
    /// <summary>来源 ID</summary>
    [FieldOffset(0x1C)] public uint SourceID;
    /// <summary>来源序列号</summary>
    [FieldOffset(0x28)] public ushort SourceSequence;
    /// <summary>未知字段</summary>
    [FieldOffset(0x2A)] public ushort Unk;
}
