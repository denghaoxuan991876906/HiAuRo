# HiAuRo Agent Guide

## What This Is

HiAuRo is a FFXIV Dalamud **全栈战斗辅助框架** (.NET 10, Dalamud.NET.Sdk 15.0.0)。提供运行时调度、ACR 接口、执行轴/事实轴/辅助轴三轴引擎、智能决策层、移动协调、ImGui+Web 双模式 UI。服务三层用户：**轴作者**编写副本数据、**ACR 作者**编写职业逻辑、**普通用户**安装即用。

## 三轴架构

执行轴和事实轴**互斥**，辅助轴**始终运行**：

```
执行轴（类 AE 时间轴）──┐
                        ├──→ AIRunner ──→ 技能输出
事实轴（Boss 时间表）──┘        ↑
                        │        │
辅助轴（始终运行，安全坐标计算）──┘  始终并行

事实轴 ──→ 智能层（决策引擎 + 移动协调）──→ ACR ──→ 技能输出 + 角色移动
执行轴 ──→ ACR（直接控制，不经过智能层）
```

- **执行轴**：类 AE 时间轴节点编排引擎，直接控制 ACR（强制技能/暂停/QT 切换），只管当前副本当前职业
- **事实轴**：Boss 技能时间表（JSON），全队视角，经过智能层分配减伤/治疗
- **辅助轴**：安全坐标计算脚本，独立于模式切换始终运行，配合事实轴驱动角色移动

**QT 开关**是全职业通用开关（爆发/爆发药/停手/自动减伤/AOE/TTK），所有 ACR 必须实现。系统通过设置开关统一控制行为。

## Build & Verify

**所有构建必须在 Windows 环境中执行。**

```
E:\DalamudPlugins\HiAuRo\build.cmd
```

WSL 下直接执行 `./build.sh`（自动通过 cmd.exe 转发到 Windows）。

## Architecture Rules

**These are non-negotiable:**

1. Keep code flat and direct. Prefer existing APIs over wrapper layers.
2. No premature abstraction — don't build for hypothetical future needs.
3. Use Chinese comments for maintenance and collaboration.
4. New capabilities are **additive** — never rewrite familiar workflows.
5. ACR interfaces stay close to AEAssist conventions for ACR author familiarity.

## Common Pitfalls

- **Don't use `Svc.ClientState.LocalPlayer`** — Dalamud API marks it obsolete. Use `IObjectTable.LocalPlayer` (live object) + `IPlayerState` (profile).
- **Don't iterate `IPartyMember.GameObject` multiple times** — expensive. Resolve once per scan.
- **Don't classify enemies by `BattleNpcSubKind.Enemy` alone** — some solo-duty allies also carry this flag. Must also check `ObjectKind`, `OwnerId`, `BuddyList`, `IsTargetable`.
- **Don't add wrapper layers around OmenTools** — DService is already the service locator. `HiAuRo.Data` is a thin forwarding facade, not a repository.
- **帧不变量饥饿（Frame Invariant Starvation）** — 每帧必须执行的操作（计时累加、数据刷新、轴状态机推进）不得放在可能被跳过的分支中。根本原因是**执行门控和数据累积未解耦**：Slot 执行/起手等门控只能阻止**发起新技能**，不能阻止数据累积和状态刷新。写提前 return 时必须确认不会遮盖不变量操作。

## OmenTools 即用即取（禁止重复造轮子）

以下能力 OmenTools 已直接提供，HiAuRo 代码中**必须直接用，不得自行封装或重新实现**：

| 需求 | 用这个 | 不要自己做 |
|------|--------|-----------|
| 对象表访问 | `DService.Instance().ObjectTable`（零分配 CachedEntry） | 自己封装 ObjectTable |
| 队伍/友方判断 | `ICharacter.StatusFlags`（PartyMember / AllianceMember / Friend 位标志） | `ObjectTable.SearchByID()` 查 OwnerID |
| 敌人判断 | `ICharacter.BattalionFlags`（Enemy = 4） | 多层 if 组合推断 |
| 玩家状态 | `LocalPlayerState.*`（职业/等级/移动/距离） | 自己读 ClientState |
| 战斗状态 | `GameState.*` + `DService.Condition.*` 扩展方法 | 自己组合 ICondition |
| 目标链 | `TargetManager.Target` 等（可读写） | 原生 `ITargetManager` |
| 技能释放 | `UseActionManager.UseAction()` | 自己封装 ActionManager |
| 帧调度 | `FrameworkManager.Reg(method, throttleMS)` | 自己写 Update 循环 |
| 距离计算 | `LocalPlayerState.DistanceToObject2D/3D`（含 hitbox） | 手算 Vector3.Distance |
| Buff 查询 | `IBattleChara.StatusList.HasStatus/TryGetStatus` | 自己遍历 StatusList |
| 对象分类 | `IObjectTable.CharactersRange`（..200，PC+BattleNPC） | 遍历全部 729 槽 |
| 伙伴查询 | 预缓存 `BuddyList` 的 EntityID 到 `HashSet<uint>` | 每对象嵌套遍历 BuddyList |
| 对象引用 | `member.GameObject as IPlayerCharacter`（直接转型） | `CreateObjectReference()` |
