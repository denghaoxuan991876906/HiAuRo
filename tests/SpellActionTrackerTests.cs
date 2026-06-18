#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.ACR;
using HiAuRo.Runtime;

Test_BeginTrack_同一Slot不重复开始();
Test_Clear_后可重新追踪同一Slot();
Test_Notify_错误SpellId不污染状态();
Console.WriteLine("=== 全部通过 ===");

void Test_BeginTrack_同一Slot不重复开始()
{
    var tracker = SpellActionTracker.Instance;
    tracker.Clear();
    var slot = new Slot();
    var spell = new Spell { Id = 100, Name = "A" };

    if (!tracker.BeginTrack(slot, spell))
        throw new Exception("首次 BeginTrack 应成功");
    if (tracker.BeginTrack(slot, spell))
        throw new Exception("同一 Slot 第二次 BeginTrack 应失败");
}

void Test_Clear_后可重新追踪同一Slot()
{
    var tracker = SpellActionTracker.Instance;
    tracker.Clear();
    var slot = new Slot();
    var spell = new Spell { Id = 200, Name = "B" };

    tracker.BeginTrack(slot, spell);
    tracker.Clear();

    if (!tracker.BeginTrack(slot, spell))
        throw new Exception("Clear 后同一 Slot 应可重新追踪");
}

void Test_Notify_错误SpellId不污染状态()
{
    var tracker = SpellActionTracker.Instance;
    tracker.Clear();
    var slot = new Slot();
    var spell = new Spell { Id = 300, Name = "C" };

    tracker.BeginTrack(slot, spell);
    tracker.Notify(SpellActionType.Effect, 301);

    if (tracker.HasFlag(300, SpellActionType.Effect))
        throw new Exception("错误 spell id 不应设置 flag");
}
