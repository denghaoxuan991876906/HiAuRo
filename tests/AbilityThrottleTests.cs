#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;

PluginConfig.EnsureInstance().AbilityIntervalMs = 500;

Test_Reserve后未到间隔不能再次开始();
Test_成功时间会继续约束后续能力技();
Test_Reset后允许重新开始();
Console.WriteLine("=== 全部通过 ===");

void Test_Reserve后未到间隔不能再次开始()
{
    AbilityThrottle.Reset();
    Data.Combat.LastAbilityUseTime = 0;

    if (!AbilityThrottle.TryReserveNow(1000))
        throw new Exception("首次 reserve 应成功");
    if (AbilityThrottle.CanStartNow(1499))
        throw new Exception("reserve 后未到间隔不应允许再次开始");
    if (!AbilityThrottle.CanStartNow(1500))
        throw new Exception("reserve 间隔结束后应允许再次开始");
}

void Test_成功时间会继续约束后续能力技()
{
    AbilityThrottle.Reset();
    Data.Combat.LastAbilityUseTime = 0;

    if (!AbilityThrottle.TryReserveNow(1000))
        throw new Exception("首次 reserve 应成功");

    AbilityThrottle.MarkSuccess(1200);

    if (AbilityThrottle.CanStartNow(1699))
        throw new Exception("成功时间后的间隔未结束前不应允许再次开始");
    if (!AbilityThrottle.CanStartNow(1700))
        throw new Exception("成功时间后的间隔结束后应允许再次开始");
}

void Test_Reset后允许重新开始()
{
    AbilityThrottle.Reset();
    Data.Combat.LastAbilityUseTime = 0;

    if (!AbilityThrottle.TryReserveNow(1000))
        throw new Exception("首次 reserve 应成功");

    AbilityThrottle.Reset();

    if (!AbilityThrottle.CanStartNow(1001))
        throw new Exception("reset 后应立即允许重新开始");
}
