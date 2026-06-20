#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Infrastructure;

Test_战斗增强_默认配置应可实例化();
Console.WriteLine("=== 全部通过 ===");

void Test_战斗增强_默认配置应可实例化()
{
    var config = new PluginConfig();

    if (config.SkillRangeExtension != 3f)
        throw new Exception("技能距离扩展默认值应为 3f");

    if (config.AnimationLockClampSeconds != 0.5f)
        throw new Exception("动画锁清理默认值应为 0.5f");

    if (config.NoDashDisplacementActionIds == null)
        throw new Exception("突进无位移列表不应为 null");
}
