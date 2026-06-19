#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Recording;

Test_RingBuffer_只保留最近帧并能取上一帧();
Test_EventWriter_默认只写事件帧();
Test_EventWriter_上一帧模式最多写两条并共享事件组();
Test_EventWriter_上一帧模式不污染缓存对象();
Test_EventFilter_GCD只记录刷新GCD时机();
Test_EventFilter_能力技只记录成功效果时机();
Console.WriteLine("=== 全部通过 ===");

void Test_RingBuffer_只保留最近帧并能取上一帧()
{
    var buffer = new CombatFrameRingBuffer(3);
    buffer.Push(MakeSnapshot(1, "cache"));
    buffer.Push(MakeSnapshot(2, "cache"));
    buffer.Push(MakeSnapshot(3, "cache"));
    buffer.Push(MakeSnapshot(4, "cache"));

    if (buffer.Count != 3)
        throw new Exception($"ring buffer 应只保留 3 帧，实际 {buffer.Count}");

    var prev = buffer.GetPrevious();
    if (prev?.SampleIndex != 3)
        throw new Exception($"上一帧应为 sampleIndex=3，实际 {prev?.SampleIndex}");
}

void Test_EventWriter_默认只写事件帧()
{
    var buffer = new CombatFrameRingBuffer(3);
    buffer.Push(MakeSnapshot(10, "cache"));

    var lines = CombatRecorderEventWriter.BuildEventSamples(
        buffer.GetLatest(),
        MakeSnapshot(11, "current"),
        includePreviousFrame: false,
        eventGroupId: "evt-1");

    if (lines.Count != 1)
        throw new Exception($"默认应只写 current，实际 {lines.Count}");
    if (lines[0].SampleRole != "current")
        throw new Exception($"默认记录角色应为 current，实际 {lines[0].SampleRole}");
    if (lines[0].EventGroupId != "evt-1")
        throw new Exception("current 未写入 eventGroupId");
}

void Test_EventWriter_上一帧模式最多写两条并共享事件组()
{
    var prev = MakeSnapshot(20, "cache");
    var current = MakeSnapshot(21, "current");

    var lines = CombatRecorderEventWriter.BuildEventSamples(
        prev,
        current,
        includePreviousFrame: true,
        eventGroupId: "evt-2");

    if (lines.Count != 2)
        throw new Exception($"上一帧模式应写 2 条，实际 {lines.Count}");
    if (lines[0].SampleRole != "prev" || lines[1].SampleRole != "current")
        throw new Exception($"sampleRole 顺序错误：{lines[0].SampleRole}, {lines[1].SampleRole}");
    if (lines.Any(x => x.EventGroupId != "evt-2"))
        throw new Exception("prev/current 应共享 eventGroupId");
}

void Test_EventWriter_上一帧模式不污染缓存对象()
{
    var cached = MakeSnapshot(30, "cache");
    cached.EventGroupId = "";

    var lines = CombatRecorderEventWriter.BuildEventSamples(
        cached,
        MakeSnapshot(31, "current"),
        includePreviousFrame: true,
        eventGroupId: "evt-3");

    if (lines.Count != 2)
        throw new Exception("上一帧模式应返回 prev + current");
    if (ReferenceEquals(cached, lines[0]))
        throw new Exception("prev 输出应复制缓存快照，不能直接复用 ring buffer 对象");
    if (cached.SampleRole != "cache" || cached.EventGroupId != "")
        throw new Exception($"缓存对象被污染：role={cached.SampleRole}, group={cached.EventGroupId}");
}

void Test_EventFilter_GCD只记录刷新GCD时机()
{
    if (CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.UseActionSuccess, isAbilityAction: false, gcdJustActivated: false))
        throw new Exception("GCD 不应在按下技能时的 UseActionSuccess 直接记录");

    if (CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.ActionEffect, isAbilityAction: false, gcdJustActivated: false))
        throw new Exception("GCD 不应在 ActionEffect 时重复记录");

    if (CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.CastStart, isAbilityAction: false, gcdJustActivated: false))
        throw new Exception("GCD 不应在 CastStart 时记录");

    if (!CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.GcdReadyAndAction, isAbilityAction: false, gcdJustActivated: true))
        throw new Exception("GCD 应只在真正刷新 GCD 计时器时记录");
}

void Test_EventFilter_能力技只记录成功效果时机()
{
    if (CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.UseActionSuccess, isAbilityAction: true, gcdJustActivated: false))
        throw new Exception("能力技不应在 UseActionSuccess 提前记录");

    if (!CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.AbilityEffect, isAbilityAction: true, gcdJustActivated: false))
        throw new Exception("能力技应在成功效果时记录");

    if (CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.QueuedActionChanged, isAbilityAction: true, gcdJustActivated: false))
        throw new Exception("QueuedActionChanged 不应落盘");

    if (CombatRecorder.ShouldCaptureEventForTests(CombatRecorderEventType.ActionRejected, isAbilityAction: true, gcdJustActivated: false))
        throw new Exception("ActionRejected 不应进入技能采样日志");
}

static CombatFrameSnapshot MakeSnapshot(long sampleIndex, string role)
{
    return new CombatFrameSnapshot
    {
        Timestamp = DateTimeOffset.UtcNow,
        SampleIndex = sampleIndex,
        SessionId = "session-test",
        EventGroupId = "",
        EventType = role,
        SampleRole = role,
        Source = "unknown",
        Job = 25,
        ActionId = 100,
        ActionName = "Test Action"
    };
}
