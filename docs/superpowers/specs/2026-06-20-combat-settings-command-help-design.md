# HiAuRo 战斗设置与命令帮助模块 设计文档

**日期**: 2026-06-20
**状态**: 待用户审阅
**关联需求**: 新增左侧栏战斗设置模块、整合战斗相关配置、增加战斗增强功能与统一命令帮助

---

## 1. 背景

当前 HiAuRo 的战斗相关配置分散在多个位置：

- `设置` 页同时承载界面设置、战斗参数、身位显示
- `ACR 列表` 页混入了目标选择器
- `/hi` 命令只有零散的帮助字符串，没有统一的命令说明入口

本次目标是把战斗相关配置集中整理为一个明确的左侧栏模块，并补齐以下战斗增强功能：

- 技能距离扩展
- 防击退
- 突进无位移
- 动画锁清理

同时新增一个独立的命令帮助模块，统一展示现有和新增的 `/hi` 命令。

---

## 2. 已确认范围

### 2.1 左侧栏结构

新增两个左侧栏模块：

- `战斗设置`
- `命令帮助`

### 2.2 战斗设置布局

采用单页卡片分组方案，不增加模块内二级侧栏，也不增加内部子页签。

### 2.3 现有内容迁移

- `目标选择器` 从 `ACR 列表` 迁入 `战斗设置`
- `战斗录制` 保持在 `副本` 模块，不迁移
- `战斗参数` 与 `身位显示` 从 `设置` 页迁入 `战斗设置`

### 2.4 技能距离语义

“增加攻击距离”确认解释为：

- **仅扩展技能可释放距离**
- **不扩大目标圈**
- **不改变目标碰撞半径显示**

### 2.5 突进无位移语义

`突进无位移` 需要支持：

- 全局开关
- 黑名单 / 白名单 模式
- 按 `actionId` 维护列表

---

## 3. 左侧模块设计

## 3.1 战斗设置模块

新增 `CombatSettingsTabPage`，集中承载战斗相关配置，内部采用 4 张卡片：

1. `基础参数`
2. `目标选择器`
3. `战斗增强`
4. `身位显示`

### 3.1.1 基础参数

承载以下现有字段：

- `ActionQueueInMs`
- `MaxAbilityTimesInGcd`
- `AbilityIntervalMs`
- `AoeCount`
- `SelfAxisRole`

说明：

- 现有 `AttackRange` 不继续作为这个区域的可编辑项
- 本次会从 UI 中移除旧 `AttackRange`
- 新的“技能距离扩展”单独归入 `战斗增强`

### 3.1.2 目标选择器

从 `AcrListTabPage` 迁入以下现有配置：

- `TargetSelectorEnabled`
- `TargetAutoSelectOnCountdown`
- `TargetSelectMode`
- `TargetSearchRange`
- `TargetKeepCurrent`
- `TargetDisableOnDeath`
- `TargetExcludeDummies`
- `TargetExcludeNonHostile`
- `TargetPreferAggroMarked`

迁移后：

- `ACR 列表` 页只保留职业信息与 ACR 选择
- 目标选择器不再混在 ACR 配置页中

### 3.1.3 战斗增强

集中展示 4 个新增功能：

- 技能距离扩展
- 防击退
- 突进无位移
- 动画锁清理

每个功能在 UI 上都应包含：

- 当前启用状态
- 参数配置（如适用）
- 简短说明
- 对应命令提示

### 3.1.4 身位显示

从 `SettingsTabPage` 迁入现有显示配置：

- `ShowPositional`
- `ShowTargetHitbox`
- `ShowAutoAttackRange`

迁移后：

- `设置` 页只保留界面、悬浮窗、GitHub 同步等非战斗设置

## 3.2 命令帮助模块

新增 `CommandHelpTabPage`，作为独立左侧模块，不与 `战斗设置` 混合。

命令帮助模块统一展示所有 `/hi` 命令，建议分为以下分组：

- `基础运行`
- `副本/轴`
- `目标选择器`
- `战斗增强`
- `工具与调试`

每条命令展示内容包括：

- 语法
- 功能说明
- 参数范围
- 示例

---

## 4. 命令树设计

## 4.1 保留现有命令树

以下命令保持兼容，不改名：

- `/hi on|off|toggle|status`
- `/hi panel`
- `/hi reload`
- `/hi fact`
- `/hi assist [load|unload]`
- `/hi gallery`
- `/hi catalog [export|upload]`
- `/hi target ...`

## 4.2 新增命令入口

新增两个入口：

- `/hi combat ...`
- `/hi help` 或 `/hi commands`

其中：

- `/hi combat ...` 用于战斗增强功能
- `/hi help` / `/hi commands` 用于打开命令帮助模块

## 4.3 `/hi combat` 子命令

建议命令树如下：

```text
/hi combat status

/hi combat range on|off|toggle|status
/hi combat range value <0-10>

/hi combat knockback on|off|toggle|status

/hi combat dash on|off|toggle|status
/hi combat dash mode blacklist|whitelist
/hi combat dash list
/hi combat dash add <actionId>
/hi combat dash remove <actionId>

/hi combat animlock on|off|toggle|status
/hi combat animlock value <seconds>
```

说明：

- `range` 负责技能距离扩展
- `knockback` 负责防击退
- `dash` 负责突进无位移及其黑白名单
- `animlock` 负责动画锁清理

## 4.4 `/hi target` 子命令

继续沿用现有命令树：

```text
/hi target on|off|toggle|status
/hi target logic <mode>
/hi target range <5-50>
/hi target keep on|off
/hi target dummy on|off
/hi target countdown on|off
/hi target hostile on|off
/hi target death on|off
/hi target aggro on|off
```

原因：

- 已经存在用户习惯
- 本次只是迁 UI，不改这组功能的命令命名

---

## 5. 战斗增强功能语义

## 5.1 技能距离扩展

### 目标语义

“技能距离扩展”定义为：

- 对技能的可释放距离增加额外范围
- 影响真实可放判定
- 不只是本地辅助显示判断

### 不采用的方案

不采用仅修改 `SpellHelper.IsInRange()` 的方案，因为那只会影响：

- 本地辅助判定
- 调试输出

而不能覆盖：

- `GetActionStatus`
- `UseAction`
- 原生可释放判断链路

### 技术路线

参考 `XSZToolbox` 的独立扩展链路，使用独立 runtime service 实现，不与目标圈扩展混合。

### 参数

- `EnableSkillRangeExtension`
- `SkillRangeExtension`

建议范围：

- `0` 到 `10`

## 5.2 防击退

### 目标语义

将击退长度压为 `0`，不增加额外的战斗状态推断逻辑。

### 参数

- `EnableAntiKnockback`

## 5.3 突进无位移

### 目标语义

不是“所有技能都无位移”，而是：

- 仅对会改变角色位置的技能生效
- 通过动作预记录 + 位移计算链路进行匹配

### 配置模型

- `EnableNoDashDisplacement`
- `NoDashDisplacementFilterMode`
- `NoDashDisplacementActionIds`

其中 `NoDashDisplacementFilterMode`：

- `Blacklist`
- `Whitelist`

### 运行时语义

- 黑名单模式：列表内技能保留原始位移，其余位移技能无位移
- 白名单模式：仅列表内技能无位移，其余位移技能保留原始位移

### UI 要求

- 可输入 `actionId`
- 可解析并显示技能名
- 可查看当前列表
- 可删除已有条目

## 5.4 动画锁清理

### 目标语义

动画锁功能定义为：

- 将动画锁时间压到不高于配置值
- 不强制恒定为 `0`

这与用户提供的 `MathF.Min(current, configuredValue)` 逻辑一致。

### 参数

- `EnableAnimationLockClamp`
- `AnimationLockClampSeconds`

默认值建议：

- `0.50` 或 `0.60`

不建议默认 `0`

### 覆盖链路

需要同时处理：

- `ActionManagerUpdate`
- `ProcessPacketActionEffect`

---

## 6. 配置设计

## 6.1 保持现有主配置结构

本次不做 `PluginConfig` 的大规模嵌套重构。

原因：

- 本次改动已经同时涉及 UI 模块拆分、命令整合、Hook 功能接入
- 若再同时改写整份主配置 JSON 结构，会显著增加迁移风险

因此：

- 继续使用现有 `PluginConfig`
- 在其上追加本次所需字段
- 旧字段继续兼容读取

## 6.2 继续沿用的现有字段

直接复用现有主配置字段：

- `ActionQueueInMs`
- `MaxAbilityTimesInGcd`
- `AbilityIntervalMs`
- `AoeCount`
- `SelfAxisRole`
- 全部 `Target*`
- `ShowPositional`
- `ShowTargetHitbox`
- `ShowAutoAttackRange`

## 6.3 新增字段

新增战斗增强相关字段：

```csharp
public bool EnableSkillRangeExtension { get; set; }
public float SkillRangeExtension { get; set; } = 3f;

public bool EnableAntiKnockback { get; set; }

public bool EnableNoDashDisplacement { get; set; }
public NoDashDisplacementFilterMode NoDashDisplacementFilterMode { get; set; } = NoDashDisplacementFilterMode.Blacklist;
public List<uint> NoDashDisplacementActionIds { get; set; } = [];

public bool EnableAnimationLockClamp { get; set; }
public float AnimationLockClampSeconds { get; set; } = 0.5f;
```

## 6.4 旧 `AttackRange` 处理

现有：

```csharp
public float AttackRange { get; set; } = 25f;
```

本次处理原则：

- 不复用为新功能
- 不再在 UI 中展示
- 暂时保留字段本身，避免旧配置文件读写风险

即：

- 它作为遗留字段继续存在
- 新“技能距离扩展”完全使用新的字段集

---

## 7. 运行时服务设计

## 7.1 总入口

新增统一管理器：

- `CombatEnhancementManager`

职责：

- 初始化与释放各子服务
- 读取配置并同步启停状态
- 为 UI / 命令层提供统一刷新入口

## 7.2 子服务拆分

新增 4 个独立 runtime service：

- `SkillRangeExtensionService`
- `AntiKnockbackService`
- `NoDashDisplacementService`
- `AnimationLockClampService`

设计原则：

- Hook 逻辑不写进 `CommandMgr`
- Hook 逻辑不写进 `TabPage`
- UI 仅改配置
- 命令仅改配置并触发同步

## 7.3 OmenTools 复用要求

实现时优先使用已有 OmenTools 能力：

- `CompSig`
- `GetHook<T>()`
- `UseActionManager.RegPreUseAction`
- `DService.Instance().Data`
- `DService.Instance().SigScanner`

不重复造一套新的低层 Hook 基础设施。

---

## 8. UI 与命令帮助数据源

## 8.1 命令帮助单一真源

新增一个统一的命令目录数据源，例如：

- `CommandHelpCatalog`

每条命令项至少包含：

- `Group`
- `Syntax`
- `Description`
- `Example`

## 8.2 数据复用关系

- `CommandHelpTabPage` 从 `CommandHelpCatalog` 读取并渲染
- `/hi help` 与 `/hi commands` 的聊天输出也从同一目录生成

这样可以避免：

- UI 帮助与聊天帮助各写一份
- 后续加新命令时两边漂移

---

## 9. 改动清单

## 9.1 新增 UI

- `HiAuRo/UI/Tabs/CombatSettingsTabPage.cs`
- `HiAuRo/UI/Tabs/CommandHelpTabPage.cs`

## 9.2 调整现有 UI

- `HiAuRo/UI/MainWindowNavigation.cs`
- `HiAuRo/UI/MainWindow.cs`
- `HiAuRo/UI/Tabs/SettingsTabPage.cs`
- `HiAuRo/UI/Tabs/AcrListTabPage.cs`

## 9.3 新增运行时与配置支持

- `HiAuRo/Runtime/CombatEnhancementManager.cs`
- `HiAuRo/Runtime/CombatEnhancements/SkillRangeExtensionService.cs`
- `HiAuRo/Runtime/CombatEnhancements/AntiKnockbackService.cs`
- `HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs`
- `HiAuRo/Runtime/CombatEnhancements/AnimationLockClampService.cs`
- `HiAuRo/Command/CommandHelpCatalog.cs`

## 9.4 调整命令系统

- `HiAuRo/Command/CommandMgr.cs`

## 9.5 调整主配置

- `HiAuRo/Infrastructure/PluginConfig.cs`

---

## 10. 验证方案

## 10.1 可单独验证的纯逻辑

采用 file-based tests，覆盖以下内容：

- `combat` 子命令解析
- 命令帮助目录分组与展示数据
- 突进无位移黑白名单判定
- `actionId` 到技能信息 / 时间线信息的解析逻辑

## 10.2 不做单测的副作用路径

不对以下内容编写单测：

- Hook 挂载
- 游戏内位移/击退/动画锁效果
- 实际技能释放链路

这些改为：

- Windows 构建验证
- 进游戏手动验证

## 10.3 通过标准

1. 新左侧模块显示正确
2. `战斗设置` 页面内容分组符合设计
3. `命令帮助` 页面能完整展示现有与新增命令
4. `/hi combat ...` 子命令可正确改配置与回显状态
5. `/hi target ...` 原有命令保持兼容
6. 文档与命令帮助内容一致
7. Windows 下构建通过

---

## 11. 不在本次范围

本次明确不包含：

- 目标圈扩展
- 技能距离覆盖（override）模式
- 突进无位移以外的通用位移 Hack 扩展
- 动画锁按职业/技能细粒度差异化配置
- 对整份 `PluginConfig` 做嵌套结构重构
- 把战斗录制迁出 `副本` 模块

---

## 12. 风险与对策

| 风险 | 对策 |
|------|------|
| `PluginConfig` 同时迁 UI 与加新字段，容易引入语义混淆 | 旧字段只迁 UI，不强行复用 `AttackRange` |
| Hook 功能混进命令/UI 层后会失控 | 统一拆到 `CombatEnhancementManager` + 子服务 |
| `/hi` 命令继续膨胀难维护 | 新功能统一收口到 `/hi combat`，命令帮助走统一目录 |
| 突进无位移黑白名单逻辑容易与技能时间线判定错位 | 复用 `PreUseAction + 运行时缓存 + 时间线解析` 链路 |
| 动画锁默认值过激造成副作用 | 默认采用 `0.50` 或 `0.60`，不默认清零 |

---

*Last updated: 2026-06-20*
