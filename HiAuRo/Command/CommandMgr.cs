using Dalamud.Game.Command;
using HiAuRo.Execution;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;
using HiAuRo.Runtime.CombatEnhancements;

namespace HiAuRo.Command;

/// <summary>
/// /hi 命令行系统
/// </summary>
public static class CommandMgr
{
    private const string MainCommand = "/hi";

    internal static void ApplyCombatCommandForTests(PluginConfig cfg, string rawArgs)
    {
        ApplyCombatCommandCore(cfg, rawArgs, null);
    }

    internal static bool ReloadAcr(
        bool basicAcrEnabled,
        Func<bool> reloadBasicAcr,
        Action reloadInstalledAcr)
    {
        if (basicAcrEnabled)
            return reloadBasicAcr();

        reloadInstalledAcr();
        return true;
    }

    public static void Init()
    {
        DService.Instance().Command.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = CommandHelpCatalog.MainHelpMessage
        });
    }

    public static void Shutdown()
    {
        DService.Instance().Command.RemoveHandler(MainCommand);
    }

    private static void HandleTargetCommand(string sub, string? value = null)
    {
        var cfg = PluginConfig.Instance;
        var chat = DService.Instance().Chat;

        switch (sub)
        {
            case "on":
                cfg.TargetSelectorEnabled = true;
                cfg.Save();
                chat.Print("[HiAuRo] 目标选择器: 已启用");
                break;
            case "off":
                cfg.TargetSelectorEnabled = false;
                cfg.Save();
                chat.Print("[HiAuRo] 目标选择器: 已禁用");
                break;
            case "toggle":
                cfg.TargetSelectorEnabled = !cfg.TargetSelectorEnabled;
                cfg.Save();
                chat.Print($"[HiAuRo] 目标选择器: {(cfg.TargetSelectorEnabled ? "已启用" : "已禁用")}");
                break;
            case "status":
                chat.Print($"[HiAuRo] 目标选择器: {(cfg.TargetSelectorEnabled ? "启用" : "禁用")}");
                chat.Print($"  倒计时自动选: {(cfg.TargetAutoSelectOnCountdown ? "是" : "否")}");
                chat.Print($"  逻辑: {cfg.TargetSelectMode}");
                chat.Print($"  范围: {cfg.TargetSearchRange:F0}");
                chat.Print($"  保持目标: {(cfg.TargetKeepCurrent ? "是" : "否")}");
                chat.Print($"  排除木人: {(cfg.TargetExcludeDummies ? "是" : "否")}");
                chat.Print($"  排除非敌对: {(cfg.TargetExcludeNonHostile ? "是" : "否")}");
                chat.Print($"  死亡禁用: {(cfg.TargetDisableOnDeath ? "是" : "否")}");
                chat.Print($"  优先头标: {(cfg.TargetPreferAggroMarked ? "是" : "否")}");
                break;
            case "logic":
                if (value != null && Enum.TryParse<TargetSelectMode>(value, out var mode))
                {
                    cfg.TargetSelectMode = mode;
                    cfg.Save();
                    chat.Print($"[HiAuRo] 选择逻辑: {mode}");
                }
                else
                {
                    chat.Print($"[HiAuRo] 可用逻辑: {string.Join(" / ", Enum.GetNames<TargetSelectMode>())}");
                }
                break;
            case "range":
                if (value != null && float.TryParse(value, out var r))
                {
                    cfg.TargetSearchRange = Math.Clamp(r, 5f, 50f);
                    cfg.Save();
                    chat.Print($"[HiAuRo] 索敌范围: {cfg.TargetSearchRange:F0}");
                }
                else
                {
                    chat.Print($"[HiAuRo] 用法: /hi target range <5-50>");
                }
                break;
            case "keep":
                if (value is "on" or "off")
                {
                    cfg.TargetKeepCurrent = value == "on";
                    cfg.Save();
                    chat.Print($"[HiAuRo] 有目标时不切换: {(cfg.TargetKeepCurrent ? "是" : "否")}");
                }
                else
                {
                    chat.Print("[HiAuRo] 用法: /hi target keep on|off");
                }
                break;
            case "dummy":
                if (value is "on" or "off")
                {
                    cfg.TargetExcludeDummies = value == "on";
                    cfg.Save();
                    chat.Print($"[HiAuRo] 排除木人: {(cfg.TargetExcludeDummies ? "是" : "否")}");
                }
                else
                {
                    chat.Print("[HiAuRo] 用法: /hi target dummy on|off");
                }
                break;
            case "countdown":
                if (value is "on" or "off")
                {
                    cfg.TargetAutoSelectOnCountdown = value == "on";
                    cfg.Save();
                    chat.Print($"[HiAuRo] 倒计时自动选目标: {(cfg.TargetAutoSelectOnCountdown ? "是" : "否")}");
                }
                else
                    chat.Print("[HiAuRo] 用法: /hi target countdown on|off");
                break;
            case "hostile":
                if (value is "on" or "off")
                {
                    cfg.TargetExcludeNonHostile = value == "on";
                    cfg.Save();
                    chat.Print($"[HiAuRo] 排除非敌对目标: {(cfg.TargetExcludeNonHostile ? "是" : "否")}");
                }
                else
                    chat.Print("[HiAuRo] 用法: /hi target hostile on|off");
                break;
            case "death":
                if (value is "on" or "off")
                {
                    cfg.TargetDisableOnDeath = value == "on";
                    cfg.Save();
                    chat.Print($"[HiAuRo] 死亡自动禁用: {(cfg.TargetDisableOnDeath ? "是" : "否")}");
                }
                else
                    chat.Print("[HiAuRo] 用法: /hi target death on|off");
                break;
            case "aggro":
                if (value is "on" or "off")
                {
                    cfg.TargetPreferAggroMarked = value == "on";
                    cfg.Save();
                    chat.Print($"[HiAuRo] 优先攻击头标: {(cfg.TargetPreferAggroMarked ? "是" : "否")}");
                }
                else
                    chat.Print("[HiAuRo] 用法: /hi target aggro on|off");
                break;
        }
    }

    private static bool ApplyCombatCommand(PluginConfig cfg, string rawArgs, Dalamud.Plugin.Services.IChatGui? chat)
    {
        Action<string>? print = chat is null ? null : message => chat.Print(message);
        return ApplyCombatCommandCore(cfg, rawArgs, print);
    }

    private static bool ApplyCombatCommandCore(PluginConfig cfg, string rawArgs, Action<string>? print)
    {
        var args = rawArgs.Trim().ToLowerInvariant();

        switch (args)
        {
            case "status":
                print?.Invoke(BuildCombatStatusMessage(cfg));
                return true;
            case "range on":
                cfg.EnableSkillRangeExtension = true;
                print?.Invoke("[HiAuRo] 技能距离扩展: 已启用");
                return true;
            case "range off":
                cfg.EnableSkillRangeExtension = false;
                print?.Invoke("[HiAuRo] 技能距离扩展: 已禁用");
                return true;
            case "range toggle":
                cfg.EnableSkillRangeExtension = !cfg.EnableSkillRangeExtension;
                print?.Invoke($"[HiAuRo] 技能距离扩展: {(cfg.EnableSkillRangeExtension ? "已启用" : "已禁用")}");
                return true;
            case "range status":
                print?.Invoke($"[HiAuRo] 技能距离扩展: {(cfg.EnableSkillRangeExtension ? "启用" : "禁用")} | 值: {cfg.SkillRangeExtension:F1}");
                return true;
            case "knockback on":
                cfg.EnableAntiKnockback = true;
                print?.Invoke("[HiAuRo] 防击退: 已启用");
                return true;
            case "knockback off":
                cfg.EnableAntiKnockback = false;
                print?.Invoke("[HiAuRo] 防击退: 已禁用");
                return true;
            case "knockback toggle":
                cfg.EnableAntiKnockback = !cfg.EnableAntiKnockback;
                print?.Invoke($"[HiAuRo] 防击退: {(cfg.EnableAntiKnockback ? "已启用" : "已禁用")}");
                return true;
            case "knockback status":
                print?.Invoke($"[HiAuRo] 防击退: {(cfg.EnableAntiKnockback ? "启用" : "禁用")}");
                return true;
            case "dash on":
                cfg.EnableNoDashDisplacement = true;
                print?.Invoke("[HiAuRo] 冲锋不位移: 已启用");
                return true;
            case "dash off":
                cfg.EnableNoDashDisplacement = false;
                print?.Invoke("[HiAuRo] 冲锋不位移: 已禁用");
                return true;
            case "dash toggle":
                cfg.EnableNoDashDisplacement = !cfg.EnableNoDashDisplacement;
                print?.Invoke($"[HiAuRo] 冲锋不位移: {(cfg.EnableNoDashDisplacement ? "已启用" : "已禁用")}");
                return true;
            case "dash status":
                print?.Invoke($"[HiAuRo] 冲锋不位移: {(cfg.EnableNoDashDisplacement ? "启用" : "禁用")} | 模式: {cfg.NoDashDisplacementFilterMode} | 数量: {cfg.NoDashDisplacementActionIds.Count}");
                return true;
            case "dash mode blacklist":
                cfg.NoDashDisplacementFilterMode = NoDashDisplacementFilterMode.Blacklist;
                print?.Invoke("[HiAuRo] 冲锋不位移过滤模式: Blacklist");
                return true;
            case "dash mode whitelist":
                cfg.NoDashDisplacementFilterMode = NoDashDisplacementFilterMode.Whitelist;
                print?.Invoke("[HiAuRo] 冲锋不位移过滤模式: Whitelist");
                return true;
            case "dash list":
                print?.Invoke($"[HiAuRo] 冲锋不位移列表: {(cfg.NoDashDisplacementActionIds.Count == 0 ? "(空)" : string.Join(", ", cfg.NoDashDisplacementActionIds))}");
                return true;
            case "animlock on":
                cfg.EnableAnimationLockClamp = true;
                print?.Invoke("[HiAuRo] 动画锁清理: 已启用");
                return true;
            case "animlock off":
                cfg.EnableAnimationLockClamp = false;
                print?.Invoke("[HiAuRo] 动画锁清理: 已禁用");
                return true;
            case "animlock toggle":
                cfg.EnableAnimationLockClamp = !cfg.EnableAnimationLockClamp;
                print?.Invoke($"[HiAuRo] 动画锁清理: {(cfg.EnableAnimationLockClamp ? "已启用" : "已禁用")}");
                return true;
            case "animlock status":
                print?.Invoke($"[HiAuRo] 动画锁清理: {(cfg.EnableAnimationLockClamp ? "启用" : "禁用")} | 值: {cfg.AnimationLockClampSeconds:F1}");
                return true;
        }

        if (args.StartsWith("range value ") && float.TryParse(args["range value ".Length..], out var range))
        {
            cfg.SkillRangeExtension = Math.Clamp(range, 0f, 10f);
            print?.Invoke($"[HiAuRo] 技能距离扩展值: {cfg.SkillRangeExtension:F1}");
            return true;
        }

        if (args.StartsWith("animlock value ") && float.TryParse(args["animlock value ".Length..], out var clamp))
        {
            cfg.AnimationLockClampSeconds = Math.Clamp(clamp, 0f, 2f);
            print?.Invoke($"[HiAuRo] 动画锁清理值: {cfg.AnimationLockClampSeconds:F1}");
            return true;
        }

        if (args.StartsWith("dash add ") && uint.TryParse(args["dash add ".Length..], out var addId))
        {
            if (NoDashDisplacementService.TryAddActionId(cfg.NoDashDisplacementActionIds, addId))
                print?.Invoke($"[HiAuRo] 已添加冲锋技能: {addId}");
            else
                print?.Invoke($"[HiAuRo] 冲锋技能添加失败: {addId}");
            return true;
        }

        if (args.StartsWith("dash remove ") && uint.TryParse(args["dash remove ".Length..], out var removeId))
        {
            if (NoDashDisplacementService.RemoveActionId(cfg.NoDashDisplacementActionIds, removeId))
                print?.Invoke($"[HiAuRo] 已移除冲锋技能: {removeId}");
            else
                print?.Invoke($"[HiAuRo] 冲锋技能不存在: {removeId}");
            return true;
        }

        return false;
    }

    private static string BuildCombatStatusMessage(PluginConfig cfg)
        => $"[HiAuRo] 战斗增强 | 距离扩展: {(cfg.EnableSkillRangeExtension ? "启用" : "禁用")} ({cfg.SkillRangeExtension:F1}) | 防击退: {(cfg.EnableAntiKnockback ? "启用" : "禁用")} | 冲锋不位移: {(cfg.EnableNoDashDisplacement ? "启用" : "禁用")} [{cfg.NoDashDisplacementFilterMode}, {cfg.NoDashDisplacementActionIds.Count}] | 动画锁清理: {(cfg.EnableAnimationLockClamp ? "启用" : "禁用")} ({cfg.AnimationLockClampSeconds:F1})";

    private static void OnCommand(string command, string arguments)
    {
        var args = arguments.Trim().ToLower();

        switch (args)
        {
            case "":
                Plugin.Instance.ToggleMainWindow();
                break;
            case "help":
            case "commands":
                Plugin.Instance.OpenMainWindowRoute("command_help");
                break;
            case "on":
                Runtime.RuntimeCore.Start();
                DService.Instance().Chat.Print("[HiAuRo] 已启用");
                break;
            case "off":
                Runtime.RuntimeCore.Stop();
                DService.Instance().Chat.Print("[HiAuRo] 已禁用");
                break;
            case "toggle":
                if (Runtime.RuntimeCore.IsRunning)
                    Runtime.RuntimeCore.Stop();
                else
                    Runtime.RuntimeCore.Start();
                DService.Instance().Chat.Print($"[HiAuRo] {(Runtime.RuntimeCore.IsRunning ? "已启用" : "已禁用")}");
                break;
            case "status":
                var state = Runtime.CombatContext.CurrentState;
                var running = Runtime.RuntimeCore.IsRunning;
                DService.Instance().Chat.Print($"[HiAuRo] 状态: {(running ? "运行中" : "已停止")}, 战斗: {state}");
                break;
            case "panel":
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "http://localhost:5678/jobview.html",
                    UseShellExecute = true
                });
                break;
            case "reload":
                var basicAcrEnabled = PluginConfig.Instance.BasicAcrScriptEnabled;
                ReloadAcr(basicAcrEnabled, BasicAcrDevelopment.Reload, ACRLifecycle.Reload);
                if (!basicAcrEnabled)
                    DService.Instance().Chat.Print("[HiAuRo] ACR 已重新扫描");
                break;
            case "fact":
                ModeSwitch.ToggleFactAxis();
                break;
            case "assist":
            case "assist load":
                AssistAxis.Instance.LoadAssistTimeline();
                DService.Instance().Chat.Print("[HiAuRo] 辅助轴已加载");
                break;
            case "assist unload":
                AssistAxis.Instance.UnloadAssistTimeline();
                DService.Instance().Chat.Print("[HiAuRo] 辅助轴已卸载");
                break;
#if DEBUG
            case "debug":
                if (ImGuiLib.DebugPerfWindow.Instance is { } w)
                {
                    w.IsOpen = !w.IsOpen;
                    DService.Instance().Chat.Print($"[HiAuRo] 性能监控窗口已{(w.IsOpen ? "打开" : "关闭")}");
                }
                break;
#endif
            case "gallery":
            case "demo":
                Plugin.Instance.ShowDemoWindow();
                DService.Instance().Chat.Print("[HiAuRo] 组件展示窗口已打开");
                break;
            case "catalog export":
                {
                    var catalogPath = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "trigger-catalog.json");
                    if (!File.Exists(catalogPath))
                    {
                        DService.Instance().Chat.Print("[HiAuRo] 目录未生成，请先加载 ACR");
                        break;
                    }
                    var json = File.ReadAllText(catalogPath);
                    ImGui.SetClipboardText(json);
                    DService.Instance().Chat.Print($"[HiAuRo] 触发器目录已复制到剪贴板 ({json.Length} 字节)");
                }
                break;
            case "catalog upload":
                Plugin.Instance.UploadCatalogAsync().ContinueWith(
                    t => { if (t.Exception != null) DService.Instance().Log.Error($"[Command] catalog upload 失败: {t.Exception.InnerException?.Message}"); },
                    TaskContinuationOptions.OnlyOnFaulted);
                break;
            case "target":
            case "target on":
                HandleTargetCommand("on");
                break;
            case "target off":
                HandleTargetCommand("off");
                break;
            case "target toggle":
                HandleTargetCommand("toggle");
                break;
            case "target status":
                HandleTargetCommand("status");
                break;
            case "target countdown":
            case "target countdown on":
                HandleTargetCommand("countdown", "on");
                break;
            case "target countdown off":
                HandleTargetCommand("countdown", "off");
                break;
            case "target hostile":
            case "target hostile on":
                HandleTargetCommand("hostile", "on");
                break;
            case "target hostile off":
                HandleTargetCommand("hostile", "off");
                break;
            case "target death":
            case "target death on":
                HandleTargetCommand("death", "on");
                break;
            case "target death off":
                HandleTargetCommand("death", "off");
                break;
            case "target aggro":
            case "target aggro on":
                HandleTargetCommand("aggro", "on");
                break;
            case "target aggro off":
                HandleTargetCommand("aggro", "off");
                break;
            default:
                if (args.StartsWith("combat "))
                {
                    var cfg = PluginConfig.Instance;
                    if (ApplyCombatCommand(cfg, args["combat ".Length..], DService.Instance().Chat))
                    {
                        cfg.Save();
                        CombatEnhancementManager.Instance.SyncFromConfig(cfg);
                    }
                    else
                    {
                        DService.Instance().Chat.Print(CommandHelpCatalog.CombatHelpMessage);
                    }
                }
                else if (args.StartsWith("target logic "))
                {
                    var mode = args["target logic ".Length..].Trim();
                    HandleTargetCommand("logic", mode);
                }
                else if (args.StartsWith("target range "))
                {
                    var val = args["target range ".Length..].Trim();
                    HandleTargetCommand("range", val);
                }
                else if (args.StartsWith("target keep "))
                {
                    var val = args["target keep ".Length..].Trim();
                    HandleTargetCommand("keep", val);
                }
                else if (args.StartsWith("target dummy "))
                {
                    var val = args["target dummy ".Length..].Trim();
                    HandleTargetCommand("dummy", val);
                }
                else
                {
                    DService.Instance().Chat.Print(CommandHelpCatalog.MainHelpMessage);
                }
                break;
        }
    }
}
