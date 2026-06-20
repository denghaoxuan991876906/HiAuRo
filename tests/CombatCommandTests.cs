#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Command;
using HiAuRo.Infrastructure;

Test_战斗增强_默认配置应可实例化();
Test_战斗增强命令_范围值应可更新配置();
Test_战斗增强命令_动画锁值应可更新配置();
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

void Test_战斗增强命令_范围值应可更新配置()
{
    var config = new PluginConfig();
    CommandMgr.ApplyCombatCommandForTests(config, "range value 5");
    if (config.SkillRangeExtension != 5f)
        throw new Exception("range value 5 应更新技能距离扩展");
}

void Test_战斗增强命令_动画锁值应可更新配置()
{
    var config = new PluginConfig();
    CommandMgr.ApplyCombatCommandForTests(config, "animlock value 0.6");
    if (Math.Abs(config.AnimationLockClampSeconds - 0.6f) > 0.0001f)
        throw new Exception("animlock value 0.6 应更新动画锁值");
}
