#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Infrastructure;

Test_CombatRecorder_自定义技能配置默认应为空列表();
Test_CombatRecorder_自定义技能配置应可添加多个技能();
Console.WriteLine("=== 全部通过 ===");

void Test_CombatRecorder_自定义技能配置默认应为空列表()
{
    var config = new PluginConfig();
    if (config.CombatRecorderTrackedSkills == null)
        throw new Exception("自定义技能配置列表不应为 null");
    if (config.CombatRecorderTrackedSkills.Count != 0)
        throw new Exception("自定义技能配置默认应为空");
}

void Test_CombatRecorder_自定义技能配置应可添加多个技能()
{
    var config = new PluginConfig();
    config.CombatRecorderTrackedSkills.Add(new CombatRecorderTrackedSkillConfig { ActionId = 159 });
    config.CombatRecorderTrackedSkills.Add(new CombatRecorderTrackedSkillConfig { ActionId = 158 });

    if (config.CombatRecorderTrackedSkills.Count != 2)
        throw new Exception("应能保存多个自定义技能");
    if (config.CombatRecorderTrackedSkills[0].ActionId != 159 || config.CombatRecorderTrackedSkills[1].ActionId != 158)
        throw new Exception("自定义技能 ID 保存顺序不正确");
}
