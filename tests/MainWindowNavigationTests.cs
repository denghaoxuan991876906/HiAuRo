#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.UI;

Test_战斗设置_应为独立左侧模块();
Test_命令帮助_应为独立左侧模块();
Console.WriteLine("=== 全部通过 ===");

void Test_战斗设置_应为独立左侧模块()
{
    var modules = MainWindowNavigation.Modules;
    var module = modules.SingleOrDefault(m => m.Name == "战斗设置")
        ?? throw new Exception("未找到战斗设置模块");

    if (!module.Tabs.Contains("combat_settings"))
        throw new Exception("战斗设置模块应包含 combat_settings routeKey");
}

void Test_命令帮助_应为独立左侧模块()
{
    var modules = MainWindowNavigation.Modules;
    var module = modules.SingleOrDefault(m => m.Name == "命令帮助")
        ?? throw new Exception("未找到命令帮助模块");

    if (!module.Tabs.Contains("command_help"))
        throw new Exception("命令帮助模块应包含 command_help routeKey");
}
