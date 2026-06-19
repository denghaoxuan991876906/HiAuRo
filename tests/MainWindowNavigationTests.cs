#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.UI;

Test_CombatRecorder_在副本模块有独立入口();
Console.WriteLine("=== 全部通过 ===");

void Test_CombatRecorder_在副本模块有独立入口()
{
    var modules = MainWindowNavigation.Modules;
    var dutyModule = modules.SingleOrDefault(m => m.Name == "副本")
        ?? throw new Exception("未找到副本模块");

    if (!dutyModule.Tabs.Contains("recording"))
        throw new Exception("副本模块应保留原副本录制入口 recording");

    if (!dutyModule.Tabs.Contains("combat_recording"))
        throw new Exception("副本模块应包含战斗操作录制入口 combat_recording");

    if (dutyModule.Tabs.Distinct().Count() != dutyModule.Tabs.Length)
        throw new Exception("副本模块 routeKey 不应重复，否则后注册页面不可达");
}
