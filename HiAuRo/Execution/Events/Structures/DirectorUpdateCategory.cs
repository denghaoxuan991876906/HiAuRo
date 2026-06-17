namespace HiAuRo.Execution.Events.Structures;

/// <summary>Director 更新类别</summary>
public enum DirectorUpdateCategory : uint
{
    /// <summary>战斗开始</summary>
    Commence    = 0x40000001,
    /// <summary>重新开始</summary>
    Recommence  = 0x40000006,
    /// <summary>战斗完成</summary>
    Complete    = 0x40000003,
    /// <summary>团灭</summary>
    Wipe        = 0x40000005,
}
