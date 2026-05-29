# HiAuRo Agent Guide

## What This Is

HiAuRo is a FFXIV Dalamud **全栈战斗辅助框架** (.NET 10, Dalamud.NET.Sdk 15.0.0)。提供运行时调度、ACR 接口、执行轴/事实轴引擎、智能决策层、ImGui+Web 双模式 UI、副本记录与分析等功能。ACR 作者可在此基础上开发各职业的战斗逻辑，普通用户可直接使用内置的执行轴/事实轴获得完整战斗辅助体验。

## Build & Verify

**所有构建必须在 Windows 环境中执行。** 使用项目根目录的构建脚本：

```
E:\DalamudPlugins\HiAuRo\build.cmd
```

WSL 下直接执行 `./build.sh`（自动通过 cmd.exe 转发到 Windows）。
构建流程：HiAuRo 框架 → 自动打包 SDK 到本地 NuGet 源。

WSL 下通过 cmd.exe 转发：`cmd.exe /c "E:\DalamudPlugins\HiAuRo\build.cmd"`

（如 `cmd.exe` 未在 PATH 中，使用 `/mnt/c/Windows/System32/cmd.exe /c` 的完整路径。）

### 本地 NuGet 源（加速 ACR 开发）

HiAuRo 构建后自动将 `HiAuRo.Sdk.nupkg` 输出到 `E:\DalamudPlugins\local-nuget-feed\`（可通过 MSBuild 属性 `LocalNuGetFeed` 自定义）。MyACR 项目通过 `NuGet.Config` 优先从该本地源恢复，无需等待 nuget.org 更新。

**开关**：`HiAuRo/HiAuRo.csproj` 中的 `<UseLocalNuGetFeed>` 属性控制是否启用本地打包。
- `true`（默认）：构建后自动打包到本地源，ACR 秒级恢复
- `false`：跳过本地打包，ACR 走 nuget.org

也可通过命令行临时覆盖：`dotnet build ... -p:UseLocalNuGetFeed=false`

必须的目录结构：
```
E:\DalamudPlugins\
├── HiAuRo\              ← HiAuRo 框架（本仓库）
├── MyACR\                ← ACR 项目
│   └── NuGet.Config      ← 指向 ..\local-nuget-feed\ 的本地源
└── local-nuget-feed\     ← 本地 NuGet 包存放（首次构建 HiAuRo 时自动创建）
```

**首次设置**：确保 `E:\DalamudPlugins\local-nuget-feed\` 目录存在（HiAuRo 构建时会自动创建）。

**版本升级流程**：同时更新 `HiAuRo/HiAuRo.csproj` 的 `<Version>` 和 `HiAuRo.Sdk.nuspec` 的 `<version>`。MyACR 的 `HiAuRo.Sdk` 引用使用浮动版本 `0.1.*`，自动拉最新本地包，无需手动同步。


## Architecture Rules

**These are non-negotiable:**

1. Keep code flat and direct. Prefer existing APIs over wrapper layers.
2. No premature abstraction — don't build for hypothetical future needs.
3. Use Chinese comments for maintenance and collaboration.
4. New capabilities are **additive** — never rewrite familiar workflows.
5. ACR interfaces stay close to AEAssist conventions for ACR author familiarity.


## Project Layout

```
.                       ← git repo root
├── doc/                ← all planning docs (READ BEFORE CODING)
│   ├── PROJECT.md      ← charter, constraints, key decisions
│   ├── REQUIREMENTS.md ← 46 requirement IDs with traceability
│   ├── ROADMAP.md      ← current capabilities and future directions
│   ├── ARCHITECTURE.md ← layered design, data flow, interfaces
│   ├── STACK.md        ← tech stack & dependency versions
│   ├── OMEN_TOOLS_USAGE.md  ← what OmenTools provides vs what we build
│   └── AEASSIST_STUDY.md    ← AEAssist architecture reference
├── HiAuRo/             ← plugin source
│   ├── ACR/            ← interfaces, helpers, slot system, target resolvers
│   ├── Command/        ← /hi command handler
│   ├── Data/           ← game data layer (battle, combat, objects, party, target)
│   ├── Execution/      ← execution axis + trigger metadata + script compiler
│   ├── Runtime/        ← runtime core, AIRunner, ACR lifecycle, spell queue
│   ├── UI/             ← Web UI (Kestrel + CEF) + ImGui overlays
│   ├── FactAxis/       ← fact axis (spell table, timeline, fact nodes)
│   ├── Decision/       ← decision engine + decision types
│   ├── Authoring/      ← authoring server
│   ├── Infrastructure/ ← logging, config, Browsingway IPC
│   ├── Recording/      ← encounter recording
│   └── Setting/        ← settings manager
├── OmenTools/          ← Dalamud service encapsulation (submodule)
└── Browsingway/        ← CEF rendering reference (submodule)
```

## Reference Code (read-only, do not modify)

| Path | What |
|------|------|
| `OmenTools/` | OmenTools source (DService, OmenService managers) |
| `Browsingway/` | CEF rendering reference (D3D11 texture sharing) |

## Key Conventions

- **File naming**: Chinese-friendly, direct names. `BRD_GCD_强力射击.cs` not `BRDGCD.cs`. `BRDHelp.cs` not `BardDataAccessor.cs`.
- **ACR author interfaces**: `IRotationEntry` is the single entry point. `ISlotResolver` has `int Check()` + `void Build(Slot slot)`. `Slot` is an execution unit containing `List<SlotAction>`.
- **UI**: ACR authors use `IRotationUI.RegisterControls(IUiBuilder)` with declarative C# methods (AddCheckbox, AddDropdown, AddHotkey, AddTab, AddGroup, etc.). HiAuRo translates to JSON → web frontend renders HTML. Or authors set `IRotationEntry.UseCustomUi = true` and provide their own HTML files.
- **EventHandler**: 10 callback methods (OnPreCombat, OnResetBattle, OnNoTarget, OnBattleUpdate, OnSpellCastSuccess, BeforeSpell, AfterSpell, OnEnterRotation, OnExitRotation, OnTerritoryChanged). Fired by AIRunner synchronously (not via EventSystem hooks).

## Common Pitfalls

- **Don't use `Svc.ClientState.LocalPlayer`** — Dalamud API marks it obsolete. Use `IObjectTable.LocalPlayer` (live object) + `IPlayerState` (profile).
- **Don't iterate `IPartyMember.GameObject` multiple times** — expensive. Resolve once per scan.
- **Don't classify enemies by `BattleNpcSubKind.Enemy` alone** — some solo-duty allies also carry this flag. Must also check `ObjectKind`, `OwnerId`, `BuddyList`, `IsTargetable`.
- **Don't add wrapper layers around OmenTools** — DService is already the service locator. `HiAuRo.Data` is a thin forwarding facade, not a repository.

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
