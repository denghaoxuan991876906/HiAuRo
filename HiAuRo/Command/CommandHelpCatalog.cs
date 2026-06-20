namespace HiAuRo.Command;

public static class CommandHelpCatalog
{
    public sealed record CommandHelpGroup(string Name, IReadOnlyList<CommandHelpItem> Items);
    public sealed record CommandHelpItem(string Syntax, string Description, string Example);

    public static IReadOnlyList<CommandHelpGroup> GetGroups() =>
    [
        new("基础运行",
        [
            new("/hi on", "启动 HiAuRo", "/hi on"),
            new("/hi off", "停止 HiAuRo", "/hi off"),
            new("/hi toggle", "切换运行状态", "/hi toggle"),
            new("/hi status", "查看当前运行状态", "/hi status"),
        ]),
        new("副本/轴",
        [
            new("/hi fact", "切换事实轴", "/hi fact"),
            new("/hi assist load", "加载辅助轴", "/hi assist load"),
            new("/hi assist unload", "卸载辅助轴", "/hi assist unload"),
        ]),
        new("目标选择器",
        [
            new("/hi target status", "查看目标选择器状态", "/hi target status"),
            new("/hi target range <5-50>", "设置索敌范围", "/hi target range 30"),
        ]),
        new("战斗增强",
        [
            new("/hi combat status", "查看战斗增强状态", "/hi combat status"),
            new("/hi combat range on", "启用技能距离扩展", "/hi combat range on"),
            new("/hi combat knockback on", "启用防击退", "/hi combat knockback on"),
            new("/hi combat dash on", "启用突进无位移", "/hi combat dash on"),
            new("/hi combat animlock on", "启用动画锁清理", "/hi combat animlock on"),
        ]),
        new("工具与调试",
        [
            new("/hi panel", "打开面板页面", "/hi panel"),
            new("/hi reload", "重新扫描 ACR", "/hi reload"),
            new("/hi help", "打开命令帮助模块", "/hi help"),
        ]),
    ];
}
