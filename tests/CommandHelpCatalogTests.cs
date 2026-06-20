#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Command;

Test_命令帮助_应包含战斗增强分组();
Test_命令帮助_应包含combat命令();
Console.WriteLine("=== 全部通过 ===");

void Test_命令帮助_应包含战斗增强分组()
{
    var groups = CommandHelpCatalog.GetGroups();
    if (!groups.Any(g => g.Name == "战斗增强"))
        throw new Exception("命令帮助应包含战斗增强分组");
}

void Test_命令帮助_应包含combat命令()
{
    var groups = CommandHelpCatalog.GetGroups();
    var combat = groups.Single(g => g.Name == "战斗增强");
    if (!combat.Items.Any(i => i.Syntax.Contains("/hi combat status", StringComparison.Ordinal)))
        throw new Exception("命令帮助应包含 /hi combat status");
}
