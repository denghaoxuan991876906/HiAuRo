namespace HiAuRo.UI;

public sealed class CombatRecorderEventGroupView
{
    public required string EventGroupId { get; init; }
    public required string SessionId { get; init; }
    public required string EventType { get; init; }
    public required uint ActionId { get; init; }
    public required string ActionName { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public CombatRecorderSnapshotView? Prev { get; init; }
    public CombatRecorderSnapshotView? Current { get; init; }
    public HiAuRo.Recording.CombatRunnerDebugSnapshot? RunnerDebug { get; init; }
    public required string SummaryText { get; init; }
    public required CombatRecorderDiffView Diff { get; init; }
    public bool HasDiff => Diff.HasAnyDiff;
}

public sealed class CombatRecorderSnapshotView
{
    public required HiAuRo.Recording.CombatFrameSnapshot Raw { get; init; }
}

public sealed class CombatRecorderDiffView
{
    public required List<CombatRecorderDiffRow> ScalarRows { get; init; }
    public required List<CombatRecorderDiffRow> GaugeRows { get; init; }
    public required CombatRecorderBuffDiff SelfBuffDiff { get; init; }
    public required CombatRecorderBuffDiff TargetBuffDiff { get; init; }
    public bool HasAnyDiff { get; init; }
}

public sealed class CombatRecorderDiffRow
{
    public required string Label { get; init; }
    public required string Before { get; init; }
    public required string After { get; init; }
    public bool Changed { get; init; }
}

public sealed class CombatRecorderBuffDiff
{
    public required List<string> Added { get; init; }
    public required List<string> Removed { get; init; }
    public required List<string> Changed { get; init; }
    public bool HasDiff => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;
}
