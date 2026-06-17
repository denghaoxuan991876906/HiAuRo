# HiAuRo — 需求文档

## 概述

本文档定义 HiAuRo 的功能需求、非功能需求与验收标准。需求按模块组织。

## 全局交付规则

- 实现优先简单直接，不为了形式化增加层级
- 现有 API 足够时直接使用，不新增包装层
- 注释优先中文
- 新能力做加法式扩展，不做推翻式重构

---

## 宿主层

| ID | 需求 | 验收标准 |
|----|------|----------|
| HOST-01 | 单项目 Dalamud 插件工程 | `dotnet build` 通过，生成可加载的 DLL |
| HOST-02 | 宿主正确执行初始化和释放 | Dalamud 加载/卸载插件无异常 |
| HOST-03 | 启动日志可确认插件状态 | 启动时输出 `[Lifecycle]` 日志 |

## 基础设施

| ID | 需求 | 验收标准 |
|----|------|----------|
| INF-01 | 统一格式运行日志 | 开发者可通过日志定位插件状态 |
| INF-02 | 配置持久化 | 插件重启后配置不丢失 |
| INF-03 | 调试总开关 | 不修改业务代码即可开关调试输出 |

## 数据层

| ID | 需求 | 验收标准 |
|----|------|----------|
| DATA-01 | 统一暴露战斗数据入口 `HiAuRo.Data` | 上层通过 `Data.Self` / `Data.Target` / `Data.Party` / `Data.Objects` / `Data.Combat` 读取数据 |
| DATA-02 | 直读优先，必要处整理 | Self/Target/Combat 转发 OmenTools；Party/Objects 做一次扫描多视图 |
| DATA-03 | 关键字段来源可追踪 | 字段附近可见当前值来源（调试用） |
| DATA-04 | 数据就绪判断 | 用 OmenTools `GameState.IsLoggedIn`，未就绪安全返回空值 |
| DATA-05 | 职业快捷数据入口 | 按职业拆分 XXHelp.cs，为首要目标职业（BRD）提供运行时快捷读取 |

### 数据分区明细

**Self 分区**（转发 OmenTools `LocalPlayerState.*`）

| 字段 | 来源 | 说明 |
|------|------|------|
| 玩家对象 | `LocalPlayerState.Object` | IPlayerCharacter |
| 名字 | `LocalPlayerState.Name` | 角色名称 |
| 职业/等级 | `LocalPlayerState.ClassJob` / `CurrentLevel` | 当前职业与等级 |
| HP/MP | `LocalPlayerState.Object` | 实时属性 |
| 位置/朝向 | `LocalPlayerState.Object.Position` / `Rotation` | |
| 施法状态 | `DService.Condition.IsCasting()` | 是否在读条、读条技能 ID |
| 移动状态 | `LocalPlayerState.IsMoving` | |
| Buff/DOT | `LocalPlayerState.HasStatus()` | |

**Target 分区**（转发 OmenTools `TargetManager.*`）

| 字段 | 说明 |
|------|------|
| Target / FocusTarget / MouseOverTarget / SoftTarget / PreviousTarget | OmenTools 全部直接支持，可读可写 |

**Party 分区**（扫描 `DService.PartyList`，方法写在 Data.Party 里）

| 视图 | 说明 |
|------|------|
| All | 全部队员 |
| Alive / Dead | 存活/死亡 |
| Tanks / Healers / Dps | 按角色分类 |
| Nearby5y / Nearby10y / Nearby15y | 按距离分桶 |
| CastableParty | 可施法队友（排除自己与距离外） |
| CastableTanks / CastableHealers / CastableDps | 可施法角色分类 |
| CastableMainTanks | 开着盾姿的 T |
| CastableMelees / CastableRangeds | 近战/远程可施法队友 |
| CastableAlliesWithin20 / 25 / 30 | 大范围 AOE/治疗判定 |

**Objects 分区**（扫描 `DService.ObjectTable`，方法写在 Data.Objects 里）

| 视图 | 说明 |
|------|------|
| All | 全对象表 |
| Allies | 友方战斗对象 |
| Enemies | 敌方战斗对象（联合 ObjectKind + BattleNpcSubKind + OwnerId + BuddyList + IsTargetable） |
| Party | 小队成员对象 |
| Pets / Summons | 宠物与召唤物 |
| Environment | 场景/NPC 对象 |

**Combat 分区**（转发 OmenTools `GameState.*` + `DService.Condition`）

| 字段 | 来源 | 说明 |
|------|------|------|
| InCombat | `DService.Condition` | 是否在战斗中 |
| IsPvP | `GameState.IsInPVPArea` | 是否 PvP |
| IsInInstance | `GameState.IsInInstanceArea` | 是否在副本中 |
| TerritoryType | `GameState.TerritoryType` | 当前地图 ID |
| Map | `GameState.Map` | 当前 Map ID |
| ServerTime | `GameState.ServerTime` | 服务器时间 |
| DeltaTime | `GameState.DeltaTime` | 帧时间 |

**Job 数据**（XXHelp.cs）

| 文件 | 说明 |
|------|------|
| `Data/Jobs/BRDHelp.cs` | 诗人职业快捷入口：歌曲状态、DoT 监控 Buff 关注点、读条关注点 |

## 运行时核心

| ID | 需求 | 验收标准 |
|----|------|----------|
| CORE-01 | 每帧 Tick 循环 | 通过 OmenTools `FrameworkManager.Reg()` 注册，ACR 逻辑每帧驱动 |
| CORE-02 | 战斗上下文状态 | 进战斗/脱战/切图状态正确切换 |
| CORE-03 | ACR 生命周期管理 | Init / Update / Dispose 在正确的时机调用 |
| CORE-04 | 模式切换 | 支持无轴/执行轴/事实轴三种模式互斥切换 |
| CORE-05 | 基础调度能力 | 为节点推进、策略控制提供调度入口 |

## ACR 抽象

| ID | 需求 | 验收标准 |
|----|------|----------|
| ACR-01 | 职业执行器统一接口 | ACR 作者实现 `IRotationEntry.Build()` 即可接入 |
| ACR-02 | Slot Resolver 机制 | 支持 Gcd / OffGcd / Always 三种 SlotMode；Resolver.Check() 返回 int |
| ACR-03 | 一致的开发体验 | Rotation + SlotResolverData + SlotMode 等概念统一 |
| ACR-04 | 起手爆发序列 | `IOpener` 接口，OpenerMgr 管理起手逻辑 |
| ACR-05 | 技能序列 | `ISlotSequence` 接口，支持组合按键/连续技能 |
| ACR-06 | 触发器系统 | `ITriggerAction`（触发时执行的动作）和 `ITriggerCond`（触发条件判断）接口定义 |
| ACR-07 | 触发器事件处理 | `IRotationEventHandler` 接口，处理战斗事件回调 |
| ACR-08 | 技能/BUFF 常量定义 | SpellsDefine / AurasDefine，常用技能和 BUFF ID 表（中文注释） |
| ACR-09 | GCD 工具 | GCDHelper：辅助判断 GCD 剩余时间、窗口状态 |
| ACR-10 | 热键/QT 系统 | 支持热键绑定和 QT（Quick Toggle）开关；HotkeyHelper 管理热键状态 |
| ACR-11 | `/hi` 命令行 | CommandMgr 注册命令，可开关 ACR、切换职业 |
| ACR-12 | 设置管理 | SettingMgr 统一管理全局设置和职业独立设置；配置持久化 |
| ACR-13 | 悬浮控制面板 | ImGui 主面板，显示开关、职业、运行状态 |
| ACR-14 | 职业悬浮窗 | JobViewWindow，职业技能提示 + QT 开关面板 |
| ACR-15 | BRD 打样实例 | 1 GCD + 1 oGCD 技能，验证 IOpener、ISlotSequence、触发器链路完整 |
| ACR-16 | 设置 UI | 全局设置页 + 职业设置页的 UI 框架 |

## 执行轴

| ID | 需求 | 验收标准 |
|----|------|----------|
| EXEC-01 | 条件驱动的执行控制 | 根据时间/事件/状态触发节点推进 |
| EXEC-02 | 可切换 ACR 行为 | 执行轴可控制 ACR 的 AOE/单体/停手等模式 |
| EXEC-03 | 可指定技能 | 执行轴可强制 ACR 使用特定技能 |
| EXEC-04 | 节点调试能力 | 可查看当前节点、为何触发/未触发 |

## 事实轴

| ID | 需求 | 验收标准 |
|----|------|----------|
| FACT-01 | JSON 定义 Boss 技能时间线 | 节点含技能 ID、时间偏移、同步条件、分支 |
| FACT-02 | 事件同步与校准 | 根据实际战斗事件对齐时间线 |
| FACT-03 | 分支切换 | 根据 Boss 状态选择不同分支 |
| FACT-04 | 输出"战斗打到哪了" | 不直接指定按键，输出当前战斗事实状态 |

## 智能决策层

| ID | 需求 | 验收标准 |
|----|------|----------|
| AI-01 | 消费事实轴 + 运行时状态 | 读取事实轴位置、团队资源、自定义约束 |
| AI-02 | 输出策略开关 | 统一接口控制各职业 ACR 的输出策略 |
| AI-03 | 减伤/治疗明确指定 | 不模糊倾向，输出确切的减伤/治疗指令 |

## 创作工具

| ID | 需求 | 验收标准 |
|----|------|----------|
| UX-01 | 可视化事实轴编辑器 | 编辑事实轴并导出 JSON（纯前端 File System Access API） |
| UX-02 | 调试与复盘界面 | 查看时间线漂移、节点状态、修正建议 |
| UX-03 | 调优建议（非自动写入） | 以建议形式输出，人工确认 |

## HiAuRo.Helper

| ID | 需求 | 验收标准 |
|----|------|----------|
| HELPER-01 | 全 21 职业辅助库 | 独立仓库，HelperUpdater 自动下载/更新/热重载 |

---

## v2 需求（远期）

| ID | 需求 | 说明 |
|----|------|------|
| COOP-01 | 团队协同视图 | 完整的团队状态视图与跨职业策略面板 |
| AI-04 | 自适应兜底策略 | 主链路稳定后，评估偏离预案后的智能兜底 |

---

## 非功能需求

| ID | 需求 | 说明 |
|----|------|------|
| NF-01 | 性能 | 每帧数据处理 < 1ms，不明显影响游戏帧率 |
| NF-02 | 稳定性 | 插件崩溃不影响游戏本身 |
| NF-03 | 兼容性 | 跟随 Dalamud API 版本更新 |
| NF-04 | 可调试性 | 关键路径有可开关的调试日志 |
| NF-05 | 可移植性 | ACR 作者迁移学习成本低 |

---

## 不做事项

| 项目 | 原因 |
|------|------|
| 完整职业循环实现 | ACR 作者负责，HiAuRo 只做框架 |
| 第三方运行时依赖 | HiAuRo 是独立插件 |
| 自动走位/跑位 | 空间信息只提供点位 |
| 执行轴+事实轴同时运行 | 模式互斥 |
| 手写 DSL 维护事实轴 | JSON + 编辑器是长期方案 |
| 自动改写攻略 | 人工确认优先 |

---

## 需求追溯表

| ID | 模块 | 说明 |
|----|------|------|
| HOST-01~03 | 宿主层 | 插件工程、生命周期、启动日志 |
| INF-01~03 | 基础设施 | 日志、配置、调试开关 |
| DATA-01~05 | 数据层 | 统一入口、直读优先、来源追踪、就绪判断、职业快捷入口 |
| CORE-01~05 | 运行时核心 | Tick 循环、战斗上下文、ACR 生命周期、模式切换、调度 |
| ACR-01~16 | ACR 抽象 | 接口、Slot、起手、序列、触发器、事件、常量、工具、热键、命令、设置、UI、打样 |
| EXEC-01~04 | 执行轴 | 条件驱动、行为切换、技能指定、调试 |
| FACT-01~04 | 事实轴 | JSON 时间线、同步校准、分支切换、状态输出 |
| AI-01~03 | 智能决策层 | 消费事实轴、策略输出、减伤/治疗指定 |
| UX-01~03 | 创作工具 | 编辑器、调试复盘、调优建议 |
| HELPER-01 | Helper | 全职业辅助库 |

**v1 需求总数: 46 个**（不含 NF，含 NF 为 51）

---

*Last updated: 2026-05-27*
