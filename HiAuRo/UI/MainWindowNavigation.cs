namespace HiAuRo.UI;

internal static class MainWindowNavigation
{
    public static readonly ModuleDefinition[] Modules =
    [
        new("主控", "settings", ["status", "settings"]),
        new("战斗设置", "settings", ["combat_settings"]),
        new("命令帮助", "settings", ["command_help"]),
        new("ACR", "settings", ["acr_list", "acr_debug"]),
        new("调试", "bug", ["debug", "vfx_test"]),
        new("副本", "clock", ["recording", "combat_recording"]),
    ];
}

internal sealed record ModuleDefinition(string Name, string IconKey, string[] Tabs);
