using System.Numerics;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Rendering;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.UI.Tabs;

public sealed class SettingsTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public SettingsTabPage(PluginConfig config, Action saveConfig)
        : base("设置", "settings", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();

        // ── 界面设置（控制面板 + 面板布局）──
        CL.Card("界面设置", () =>
        {
            var collapsedW = (int)_config.OverlayStatusBarCollapsedW;
            if (CL.InputNumber("sb_collapsed_w", "折叠宽度", ref collapsedW, 10))
            {
                _config.OverlayStatusBarCollapsedW = collapsedW;
                _saveConfig();
            }
            var collapsedH = (int)_config.OverlayStatusBarCollapsedH;
            if (CL.InputNumber("sb_collapsed_h", "折叠高度", ref collapsedH, 5))
            {
                _config.OverlayStatusBarCollapsedH = collapsedH;
                _saveConfig();
            }

            CL.Divider();

            var ui = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
            if (ui == null)
            {
                ImGui.TextColored(Theme.Colors.TextTertiary, "请先加载 ACR 以配置 QT/热键面板");
            }
            else
            {
                var qtCols = ui.QtCols > 0 ? ui.QtCols : 4;
                if (CL.SliderInt("qt_cols", "QT 每行列数", ref qtCols, 1, 10))
                {
                    ui.QtCols = qtCols;
                    HiAuRo.Runtime.ACRLifecycle.MarkSettingsDirty();
                }

                var hkCols = ui.HkCols > 0 ? ui.HkCols : 5;
                if (CL.SliderInt("hk_cols", "热键每行列数", ref hkCols, 1, 10))
                {
                    ui.HkCols = hkCols;
                    HiAuRo.Runtime.ACRLifecycle.MarkSettingsDirty();
                }

                var hkBtn = ui.HkBtnSize > 0 ? (int)ui.HkBtnSize : 50;
                if (CL.SliderInt("hk_btnsz", "热键按钮大小(px)", ref hkBtn, 28, 80))
                {
                    ui.HkBtnSize = hkBtn;
                    HiAuRo.Runtime.ACRLifecycle.MarkSettingsDirty();
                }
            }
        });

        // ── 战斗参数 ──
        CL.Card("战斗参数", () =>
        {
            var aq = _config.ActionQueueInMs;
            var maxAb = _config.MaxAbilityTimesInGcd;
            var abInterval = _config.AbilityIntervalMs;
            var aoe = _config.AoeCount;
            var range = _config.AttackRange;
            var debug = _config.DebugEnabled;
            var changed = false;

            CL.FormRow("技能队列窗口 (ms)", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##aq_window", ref aq, 50, 100);
            });

            CL.FormRow("GCD 内能力技上限", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##max_ab", ref maxAb, 1, 10);
            });

            CL.FormRow("能力技间隔 (ms)", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##ab_interval", ref abInterval, 50, 100);
            });

            CL.FormRow("AOE 判定敌人数", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##aoe_count", ref aoe, 1, 5);
            });

            changed |= CL.Slider("attack_range", "攻击距离", ref range, 5f, 40f, "%.1f");

            CL.FormRow("Debug 日志", () =>
            {
                changed |= CL.Switch("debug_log", ref debug);
            });

            var selfAxisRole = (int)_config.SelfAxisRole;
            var selfAxisRoleNames = Enum.GetNames<AxisRole>();
            if (CL.Select("self_axis_role", "全轴自身职能", ref selfAxisRole, selfAxisRoleNames))
            {
                _config.SelfAxisRole = (AxisRole)selfAxisRole;
                changed = true;
            }
            ImGui.TextColored(Theme.Colors.TextSecondary, "执行轴/事实轴/辅助轴共用");

            if (changed)
            {
                _config.ActionQueueInMs = aq;
                _config.MaxAbilityTimesInGcd = maxAb;
                _config.AbilityIntervalMs = abInterval;
                _config.AoeCount = aoe;
                _config.AttackRange = range;
                _config.DebugEnabled = debug;
                _saveConfig();
            }
        });

        // ── 身位指示器 ──
        CL.Card("身位指示器", () =>
        {
            var showPos = _config.ShowPositional;
            var showHitbox = _config.ShowTargetHitbox;
            var showAA = _config.ShowAutoAttackRange;
            var changed = false;

            CL.FormRow("启用身位指示器", () =>
            {
                changed |= CL.Switch("show_positional", ref showPos);
            });

            if (showPos)
            {
                ImGui.Indent(16f);
                CL.FormRow("显示目标攻击范围圈", () =>
                {
                    changed |= CL.Switch("show_hitbox", ref showHitbox);
                });
                CL.FormRow("显示自动攻击范围圈", () =>
                {
                    changed |= CL.Switch("show_aa_range", ref showAA);
                });
                ImGui.Unindent(16f);
            }

            if (changed)
            {
                _config.ShowPositional = showPos;
                _config.ShowTargetHitbox = showHitbox;
                _config.ShowAutoAttackRange = showAA;
                if (!showPos)
                    PositionalVfx.Clear();
                _saveConfig();
            }
        });

        // ── 触发器目录同步 ──
        CL.Card("触发器目录同步", () =>
        {
            var ghToken = _config.GitHubToken ?? "";
            var ghRepo = _config.CatalogRepo ?? "";
            var ghBranch = _config.CatalogBranch ?? "";
            var changed = false;

            CL.FormRow("GitHub Token", () =>
            {
                ImGui.SetNextItemWidth(220);
                changed |= ImGui.InputTextWithHint("##gh_token", "ghp_... (repo 权限)", ref ghToken, 128, ImGuiInputTextFlags.Password);
            });

            CL.FormRow("仓库", () =>
            {
                ImGui.SetNextItemWidth(220);
                changed |= ImGui.InputText("##gh_repo", ref ghRepo, 128);
            });

            CL.FormRow("分支", () =>
            {
                ImGui.SetNextItemWidth(150);
                changed |= ImGui.InputText("##gh_branch", ref ghBranch, 64);
            });

            if (changed)
            {
                _config.GitHubToken = ghToken.Length > 0 ? ghToken : null;
                _config.CatalogRepo = ghRepo;
                _config.CatalogBranch = ghBranch;
                _saveConfig();
            }
        });

        // ── 外部悬浮窗 ──
        CL.Card("外部悬浮窗", () =>
        {
            var overlays = _config.Overlays;
            if (overlays is null or { Length: 0 })
            {
                ImGui.TextColored(Theme.Colors.TextTertiary, "暂无外部悬浮窗配置。");
                return;
            }

            for (int i = 0; i < overlays.Length; i++)
            {
                var ol = overlays[i];
                var overlayChanged = false;

                ImGui.PushID(i);

                CL.Collapsible(ol.Name, () =>
                {
                    var vis = ol.Visible;
                    if (CL.Switch($"ol_vis_{i}", "启用", ref vis))
                    {
                        ol.Visible = vis;
                        overlayChanged = true;
                    }

                    var url = ol.Url;
                    CL.FormRow("URL", () =>
                    {
                        ImGui.SetNextItemWidth(240);
                        if (ImGui.InputText("##ol_url", ref url, 256))
                        {
                            ol.Url = url;
                            overlayChanged = true;
                        }
                    });

                    var w = ol.Width;
                    var h = ol.Height;
                    if (CL.InputNumber($"ol_w_{i}", "宽", ref w, 10))
                    {
                        ol.Width = w;
                        overlayChanged = true;
                    }
                    ImGui.SameLine();
                    if (CL.InputNumber($"ol_h_{i}", "高", ref h, 10))
                    {
                        ol.Height = h;
                        overlayChanged = true;
                    }

                    var zoom = ol.Zoom;
                    if (CL.Slider($"ol_zoom_{i}", "缩放 %", ref zoom, 50f, 200f, "%.0f%%"))
                    {
                        ol.Zoom = zoom;
                        overlayChanged = true;
                    }

                    var locked = ol.Locked;
                    if (CL.Switch($"ol_lock_{i}", "锁定窗口", ref locked))
                    {
                        ol.Locked = locked;
                        overlayChanged = true;
                    }
                });

                ImGui.PopID();

                if (overlayChanged)
                {
                    Plugin.Instance._uiManager?.BrowsingwayIpc?.CreateOrUpdateOverlay(ol);
                    _saveConfig();
                }
            }
        });
    }
}
