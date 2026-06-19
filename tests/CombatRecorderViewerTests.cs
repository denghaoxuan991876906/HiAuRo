#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.UI;
using HiAuRo.Recording;

Test_CombatRecorder_主窗口入口应指向独立阅读器页面();
Test_Grouping_按eventGroupId聚合prev和current();
Test_Summary_生成左侧时间线摘要();
Test_Diff_提取buff增删与量谱变化();
Test_Grouping_保留GCD类型与自定义技能信息();
Console.WriteLine("=== 全部通过 ===");

void Test_CombatRecorder_主窗口入口应指向独立阅读器页面()
{
    if (!CombatRecorderViewerPageConfig.FileName.EndsWith("combat-recorder.html", StringComparison.Ordinal))
        throw new Exception("战斗录制阅读器入口应打开 combat-recorder.html");
}

void Test_Grouping_按eventGroupId聚合prev和current()
{
    var rows = new[]
    {
        MakeSnapshot("g1", "prev", 100, "火4", "GcdReadyAndAction"),
        MakeSnapshot("g1", "current", 100, "火4", "GcdReadyAndAction")
    };

    var groups = CombatRecorderViewerLogic.BuildGroups(rows);
    if (groups.Count != 1)
        throw new Exception($"应聚合为 1 个事件组，实际 {groups.Count}");
    if (groups[0].Prev == null || groups[0].Current == null)
        throw new Exception("事件组应同时包含 prev 和 current");
}

void Test_Summary_生成左侧时间线摘要()
{
    var prev = MakeSnapshot("g2", "prev", 100, "火4", "GcdReadyAndAction");
    prev.Mp = 10000;
    prev.JobGauge["astralFireStacks"] = 0;

    var current = MakeSnapshot("g2", "current", 100, "火4", "GcdReadyAndAction");
    current.Mp = 6800;
    current.JobGauge["astralFireStacks"] = 3;

    var group = CombatRecorderViewerLogic.BuildGroups([prev, current]).Single();
    if (!group.SummaryText.Contains("MP 10000 -> 6800", StringComparison.Ordinal))
        throw new Exception($"摘要应包含 MP 变化，实际 {group.SummaryText}");
    if (!group.SummaryText.Contains("astralFireStacks 0 -> 3", StringComparison.Ordinal))
        throw new Exception($"摘要应包含量谱变化，实际 {group.SummaryText}");
}

void Test_Diff_提取buff增删与量谱变化()
{
    var prev = MakeSnapshot("g3", "prev", 7561, "即刻咏唱", "AbilityEffect");
    prev.JobGauge["polyglotStacks"] = 1;
    prev.SelfBuffs.Add(new CombatBuffSnapshot { Id = 1, Name = "A", Stack = 1, RemainMs = 1000 });

    var current = MakeSnapshot("g3", "current", 7561, "即刻咏唱", "AbilityEffect");
    current.JobGauge["polyglotStacks"] = 2;
    current.SelfBuffs.Add(new CombatBuffSnapshot { Id = 2, Name = "B", Stack = 1, RemainMs = 2000 });

    var group = CombatRecorderViewerLogic.BuildGroups([prev, current]).Single();
    if (!group.Diff.GaugeRows.Any(row => row.Label == "polyglotStacks" && row.Changed))
        throw new Exception("应提取 polyglotStacks 变化");
    if (!group.Diff.SelfBuffDiff.Added.Any(item => item.Contains("B (2)", StringComparison.Ordinal)))
        throw new Exception("应识别新增 Buff");
    if (!group.Diff.SelfBuffDiff.Removed.Any(item => item.Contains("A (1)", StringComparison.Ordinal)))
        throw new Exception("应识别移除 Buff");
}

void Test_Grouping_保留GCD类型与自定义技能信息()
{
    var current = MakeSnapshot("g4", "current", 16505, "真红核爆", "GcdReadyAndAction");
    current.GcdKind = CombatRecorderGcdKind.Instant;
    current.TrackedSkills =
    [
        new CombatTrackedSkillSnapshot
        {
            ActionId = 158,
            ActionName = "魔纹步",
            CooldownRemainingMs = 45000,
            Charges = 2,
            MaxCharges = 3
        }
    ];

    var group = CombatRecorderViewerLogic.BuildGroups([current]).Single();
    if (group.Current?.Raw.GcdKind != CombatRecorderGcdKind.Instant)
        throw new Exception("阅读器分组后应保留 GCD 类型");
    if (group.Current?.Raw.TrackedSkills.Count != 1)
        throw new Exception("阅读器分组后应保留自定义技能信息");
    if (group.Current.Raw.TrackedSkills[0].ActionName != "魔纹步")
        throw new Exception("阅读器分组后应保留自定义技能名称");
}

static CombatFrameSnapshot MakeSnapshot(string groupId, string role, uint actionId, string actionName, string eventType)
{
    return new CombatFrameSnapshot
    {
        Timestamp = DateTimeOffset.Parse("2026-06-19T12:54:31+08:00"),
        SessionId = "session-test",
        EventGroupId = groupId,
        SampleRole = role,
        EventType = eventType,
        ActionId = actionId,
        ActionName = actionName,
        JobGauge = [],
        SelfBuffs = [],
        TargetBuffs = [],
        TrackedSkills = []
    };
}
