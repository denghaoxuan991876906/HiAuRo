namespace HiAuRo.ACR;

/// <summary>
/// ACR Debug 面板数据 —— 每个 SlotResolver 一帧的 Check/Build 状态快照
/// </summary>
public sealed class ResolverDebugInfo
{
    public string Name { get; init; } = "";
    public SlotMode Mode { get; init; }
    public int CheckResult { get; set; }
    public bool CheckThrew { get; set; }
    public string CheckError { get; set; } = "";
    public bool PassedWindow { get; set; }
    public bool BuiltSlot { get; set; }
    public string BuiltSkills { get; set; } = "";
}
