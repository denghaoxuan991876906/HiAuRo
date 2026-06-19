#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Runtime;

Test_TargetChange_按地址变化判定();
Console.WriteLine("=== 全部通过 ===");

void Test_TargetChange_按地址变化判定()
{
    if (EventSystem.ShouldPublishTargetChangedForTests((nint)0x1000, (nint)0x1000))
        throw new Exception("相同目标地址不应重复发布 TargetChanged");

    if (!EventSystem.ShouldPublishTargetChangedForTests((nint)0x1000, (nint)0x2000))
        throw new Exception("目标地址变化时应发布 TargetChanged");

    if (!EventSystem.ShouldPublishTargetChangedForTests((nint)0x2000, nint.Zero))
        throw new Exception("目标清空时应发布 TargetChanged");
}
