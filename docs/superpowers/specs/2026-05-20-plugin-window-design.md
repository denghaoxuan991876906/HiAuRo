# 插件窗口系统 设计文档

**日期**: 2026-05-20
**状态**: 已批准
**关联**: IP 插件窗口能力扩展

---

## 1. 背景

当前 `IPlugin` 仅有生命周期方法，无法提供独立 UI 窗口。需要新增窗口接口，让插件可以打开 ImGui 窗口，由 HiAuRo 统一管理显隐。

---

## 2. 接口设计

### IPluginWindow

```csharp
// HiAuRo/Plugin/IPluginWindow.cs
namespace HiAuRo;

public interface IPluginWindow
{
    string Title { get; }
    void Draw();
}
```

### IPlugin 扩展

```csharp
public interface IPlugin : IDisposable
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Update();
    IPluginWindow? GetWindow();  // 新增，默认返回 null
}
```

---

## 3. IPlugin 向后兼容

现有插件（如 FaPlugin）未实现 `GetWindow()` 不会编译出错 —— 接口新增方法，旧实现需要补充。方案：

- **方案 A**：给 `GetWindow()` 加 default 实现 `=> null`（C# 8+ 接口默认方法）
- **方案 B**：所有现有实现补充 `=> null`

选 A，因为 C# 接口默认方法是标准做法，无需修改现有代码。

---

## 4. PluginWindowManager

```
HiAuRo/Runtime/PluginWindowManager.cs

职责：
  - 遍历 PluginLoader.Plugins，收集有窗口的插件
  - 为每个窗口创建 Window 包装（ImGui 子类）
  - 注册到 HiAuRo 的 WindowSystem
  - 维护 IsOpen 状态
  - 提供 Toggle(name) 方法
  - 提供命令接口（/hi toggle FA）
```

---

## 5. 改动清单

| 文件 | 操作 |
|------|------|
| `Plugin/IPluginWindow.cs` | 新增 |
| `Plugin/IPlugin.cs` | 修改：加 `GetWindow()` 默认实现 |
| `Runtime/PluginWindowManager.cs` | 新增 |
| `Plugin.cs` | Init 中调用 PluginWindowManager.Init() |
| `Runtime/RuntimeCore.cs` | 不变（窗口走 ImGui Draw，不走 Tick） |
