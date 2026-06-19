using HiAuRo.Recording;

namespace HiAuRo.UI;

internal static class CombatRecorderViewerLogic
{
    public static IReadOnlyList<CombatRecorderEventGroupView> BuildGroups(IEnumerable<CombatFrameSnapshot> rows)
    {
        var groups = rows
            .GroupBy(row => row.EventGroupId)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(row => row.SampleRole == "prev" ? 0 : 1)
                    .ThenBy(row => row.Timestamp)
                    .ToList();

                var prev = ordered.FirstOrDefault(row => row.SampleRole == "prev");
                var current = ordered.FirstOrDefault(row => row.SampleRole == "current") ?? ordered.LastOrDefault();
                var anchor = current ?? prev ?? throw new InvalidOperationException("空事件组");
                var diff = BuildDiff(prev, current);

                return new CombatRecorderEventGroupView
                {
                    EventGroupId = anchor.EventGroupId,
                    SessionId = anchor.SessionId,
                    EventType = anchor.EventType,
                    ActionId = anchor.ActionId,
                    ActionName = string.IsNullOrWhiteSpace(anchor.ActionName) ? $"Action {anchor.ActionId}" : anchor.ActionName,
                    Timestamp = anchor.Timestamp,
                    Prev = prev == null ? null : new CombatRecorderSnapshotView { Raw = prev },
                    Current = current == null ? null : new CombatRecorderSnapshotView { Raw = current },
                    SummaryText = BuildSummary(anchor.ActionName, diff, prev, current),
                    Diff = diff
                };
            })
            .OrderBy(group => group.Timestamp)
            .ToList();

        return groups;
    }

    public static CombatRecorderDiffView BuildDiff(CombatRecorderEventGroupView group)
        => BuildDiff(group.Prev?.Raw, group.Current?.Raw);

    public static string BuildSummary(CombatRecorderEventGroupView group)
        => group.SummaryText;

    private static CombatRecorderDiffView BuildDiff(CombatFrameSnapshot? prev, CombatFrameSnapshot? current)
    {
        var scalarRows = new List<CombatRecorderDiffRow>();
        var gaugeRows = new List<CombatRecorderDiffRow>();
        var hasAnyDiff = false;

        AddRow(scalarRows, "HP", FormatNumber(prev?.Hp), FormatNumber(current?.Hp), ref hasAnyDiff);
        AddRow(scalarRows, "MP", FormatNumber(prev?.Mp), FormatNumber(current?.Mp), ref hasAnyDiff);
        AddRow(scalarRows, "GCD", FormatMilliseconds(prev?.GcdCooldown), FormatMilliseconds(current?.GcdCooldown), ref hasAnyDiff);
        AddRow(scalarRows, "GCD类型", FormatGcdKind(prev?.GcdKind), FormatGcdKind(current?.GcdKind), ref hasAnyDiff);
        AddRow(scalarRows, "读条", FormatBool(prev?.IsCasting), FormatBool(current?.IsCasting), ref hasAnyDiff);
        AddRow(scalarRows, "移动", FormatBool(prev?.IsMoving), FormatBool(current?.IsMoving), ref hasAnyDiff);
        AddRow(scalarRows, "连击ID", FormatNumber(prev?.LastComboSpellId), FormatNumber(current?.LastComboSpellId), ref hasAnyDiff);
        AddRow(scalarRows, "目标HP%", FormatPercent(prev?.TargetHpPercent), FormatPercent(current?.TargetHpPercent), ref hasAnyDiff);

        foreach (var key in UniqueKeys(prev?.JobGauge, current?.JobGauge))
        {
            AddRow(gaugeRows, key, FormatValue(prev?.JobGauge.GetValueOrDefault(key)), FormatValue(current?.JobGauge.GetValueOrDefault(key)), ref hasAnyDiff);
        }

        var selfBuffDiff = BuildBuffDiff(prev?.SelfBuffs, current?.SelfBuffs);
        var targetBuffDiff = BuildBuffDiff(prev?.TargetBuffs, current?.TargetBuffs);
        hasAnyDiff |= selfBuffDiff.HasDiff || targetBuffDiff.HasDiff;

        return new CombatRecorderDiffView
        {
            ScalarRows = scalarRows,
            GaugeRows = gaugeRows,
            SelfBuffDiff = selfBuffDiff,
            TargetBuffDiff = targetBuffDiff,
            HasAnyDiff = hasAnyDiff
        };
    }

    private static string BuildSummary(string actionName, CombatRecorderDiffView diff, CombatFrameSnapshot? prev, CombatFrameSnapshot? current)
    {
        var parts = new List<string>();

        if ((prev?.Mp ?? 0) != (current?.Mp ?? 0))
            parts.Add($"MP {FormatNumber(prev?.Mp)} -> {FormatNumber(current?.Mp)}");

        foreach (var key in UniqueKeys(prev?.JobGauge, current?.JobGauge))
        {
            var before = FormatValue(prev?.JobGauge.GetValueOrDefault(key));
            var after = FormatValue(current?.JobGauge.GetValueOrDefault(key));
            if (before != after)
            {
                parts.Add($"{key} {before} -> {after}");
                break;
            }
        }

        var selfCount = diff.SelfBuffDiff.Added.Count + diff.SelfBuffDiff.Removed.Count + diff.SelfBuffDiff.Changed.Count;
        if (selfCount > 0)
            parts.Add($"selfBuff {selfCount}项变化");

        var targetCount = diff.TargetBuffDiff.Added.Count + diff.TargetBuffDiff.Removed.Count + diff.TargetBuffDiff.Changed.Count;
        if (targetCount > 0)
            parts.Add($"targetBuff {targetCount}项变化");

        if ((prev?.TargetHpPercent ?? 0) != (current?.TargetHpPercent ?? 0))
            parts.Add($"target {FormatPercent(prev?.TargetHpPercent)} -> {FormatPercent(current?.TargetHpPercent)}");

        return parts.Count > 0 ? string.Join(" | ", parts) : "无关键差异";
    }

    private static CombatRecorderBuffDiff BuildBuffDiff(List<CombatBuffSnapshot>? prevBuffs, List<CombatBuffSnapshot>? currentBuffs)
    {
        var prevMap = (prevBuffs ?? []).ToDictionary(buff => buff.Id, buff => buff);
        var currentMap = (currentBuffs ?? []).ToDictionary(buff => buff.Id, buff => buff);
        var added = new List<string>();
        var removed = new List<string>();
        var changed = new List<string>();

        foreach (var key in prevMap.Keys.Union(currentMap.Keys))
        {
            var hasPrev = prevMap.TryGetValue(key, out var before);
            var hasCurrent = currentMap.TryGetValue(key, out var after);

            if (!hasPrev && hasCurrent && after != null)
            {
                added.Add($"{after.Name} ({after.Id})");
                continue;
            }

            if (hasPrev && !hasCurrent && before != null)
            {
                removed.Add($"{before.Name} ({before.Id})");
                continue;
            }

            if (before == null || after == null)
                continue;

            var diffParts = new List<string>();
            if (before.Stack != after.Stack)
                diffParts.Add($"层数 {before.Stack} -> {after.Stack}");
            if (before.RemainMs != after.RemainMs)
                diffParts.Add($"剩余 {FormatMilliseconds(before.RemainMs)} -> {FormatMilliseconds(after.RemainMs)}");

            if (diffParts.Count > 0)
                changed.Add($"{after.Name} ({after.Id}) | {string.Join(" | ", diffParts)}");
        }

        return new CombatRecorderBuffDiff
        {
            Added = added,
            Removed = removed,
            Changed = changed
        };
    }

    private static IEnumerable<string> UniqueKeys(Dictionary<string, object?>? left, Dictionary<string, object?>? right)
    {
        var leftKeys = left?.Keys ?? Enumerable.Empty<string>();
        var rightKeys = right?.Keys ?? Enumerable.Empty<string>();
        return leftKeys.Union(rightKeys).Distinct();
    }

    private static void AddRow(List<CombatRecorderDiffRow> rows, string label, string before, string after, ref bool hasAnyDiff)
    {
        var changed = before != after;
        rows.Add(new CombatRecorderDiffRow
        {
            Label = label,
            Before = before,
            After = after,
            Changed = changed
        });
        hasAnyDiff |= changed;
    }

    private static string FormatNumber(uint? value) => value?.ToString() ?? "-";

    private static string FormatBool(bool? value) => value switch
    {
        true => "是",
        false => "否",
        _ => "-"
    };

    private static string FormatPercent(float? value) => value.HasValue ? $"{value.Value * 100:F1}%" : "-";

    private static string FormatMilliseconds(float? value) => value.HasValue ? $"{Math.Round(value.Value)}ms" : "-";

    private static string FormatMilliseconds(int value) => $"{value}ms";

    private static string FormatValue(object? value) => value switch
    {
        null => "-",
        bool b => b ? "是" : "否",
        _ => value.ToString() ?? "-"
    };

    private static string FormatGcdKind(CombatRecorderGcdKind? value) => value switch
    {
        CombatRecorderGcdKind.Instant => "瞬发",
        CombatRecorderGcdKind.Casted => "读条",
        _ => "-"
    };
}
