#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Execution.Events;

Test_ActorCast_本地玩家应发布SelfCastStart();
Console.WriteLine("=== 全部通过 ===");

void Test_ActorCast_本地玩家应发布SelfCastStart()
{
    if (!GameEventHook.ShouldPublishSelfCastStartForTests(isPlayerCharacter: true, actionId: 152))
        throw new Exception("本地玩家读条开始时应发布 SelfCastStart");

    if (GameEventHook.ShouldPublishSelfCastStartForTests(isPlayerCharacter: false, actionId: 152))
        throw new Exception("非玩家目标不应发布 SelfCastStart");

    if (GameEventHook.ShouldPublishSelfCastStartForTests(isPlayerCharacter: true, actionId: 0))
        throw new Exception("actionId 为 0 时不应发布 SelfCastStart");
}
