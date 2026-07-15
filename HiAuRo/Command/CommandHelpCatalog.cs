namespace HiAuRo.Command;

public static class CommandHelpCatalog
{
    public sealed record CommandHelpGroup(string Name, IReadOnlyList<CommandHelpItem> Items);
    public sealed record CommandHelpItem(string Syntax, string Description, string Example);

    public static string MainHelpMessage =>
        "HiAuRo: /hi on|off|toggle|status|panel|reload|fact|assist [load|unload]|debug|gallery|catalog [export|upload]|target [on|off|toggle|status|logic|range|keep|dummy|countdown|hostile|death|aggro]|combat [status|range|knockback|dash|animlock]|help|commands";

    public static string CombatHelpMessage =>
        "[HiAuRo] 用法: /hi combat status | range on|off|toggle|status|value <0-10> | knockback on|off|toggle|status | dash on|off|toggle|status|mode blacklist|whitelist|list|add <actionId>|remove <actionId> | animlock on|off|toggle|status|value <0-2>";

    public static IReadOnlyList<CommandHelpGroup> GetGroups() =>
    [
        new("基础运行",
        [
            new("/hi on", "启动 HiAuRo", "/hi on"),
            new("/hi off", "停止 HiAuRo", "/hi off"),
            new("/hi toggle", "切换运行状态", "/hi toggle"),
            new("/hi status", "查看当前运行状态", "/hi status"),
            new("/hi help", "打开命令帮助模块", "/hi help"),
            new("/hi commands", "打开命令帮助模块", "/hi commands"),
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
            new("/hi target logic <mode>", "设置目标选择逻辑", "/hi target logic 距离最近"),
            new("/hi target keep on|off", "设置是否保持当前目标", "/hi target keep on"),
            new("/hi target dummy on|off", "设置是否排除木人", "/hi target dummy on"),
            new("/hi target countdown on|off", "设置倒计时自动选目标", "/hi target countdown on"),
            new("/hi target hostile on|off", "设置是否排除非敌对目标", "/hi target hostile on"),
            new("/hi target death on|off", "设置死亡时自动禁用", "/hi target death on"),
            new("/hi target aggro on|off", "设置是否优先攻击头标目标", "/hi target aggro on"),
        ]),
        new("战斗增强",
        [
            new("/hi combat status", "查看战斗增强状态", "/hi combat status"),
            new("/hi combat range on|off|toggle|status", "控制技能距离扩展开关并查看状态", "/hi combat range toggle"),
            new("/hi combat range value <0-10>", "设置技能距离扩展值", "/hi combat range value 3"),
            new("/hi combat knockback on|off|toggle|status", "控制防击退开关并查看状态", "/hi combat knockback toggle"),
            new("/hi combat dash on|off|toggle|status", "控制冲锋不位移开关并查看状态", "/hi combat dash toggle"),
            new("/hi combat dash mode blacklist|whitelist", "设置冲锋不位移黑白名单模式", "/hi combat dash mode blacklist"),
            new("/hi combat dash list", "查看冲锋不位移技能列表", "/hi combat dash list"),
            new("/hi combat dash add <actionId>", "添加冲锋不位移技能", "/hi combat dash add 123"),
            new("/hi combat dash remove <actionId>", "移除冲锋不位移技能", "/hi combat dash remove 123"),
            new("/hi combat animlock on|off|toggle|status", "控制动画锁清理开关并查看状态", "/hi combat animlock toggle"),
            new("/hi combat animlock value <0-2>", "设置动画锁上限值", "/hi combat animlock value 0.5"),
        ]),
        new("工具与调试",
        [
            new("/hi panel", "打开面板页面", "/hi panel"),
            new("/hi reload", "重载 ACR（开发模式仅重编译 Basic）", "/hi reload"),
            new("/hi gallery", "打开组件展示窗口", "/hi gallery"),
#if DEBUG
            new("/hi debug", "切换调试性能窗口", "/hi debug"),
#endif
        ]),
    ];
}
