# ACR 设置系统重构设计文档

## 1. 目标

重构 HiAuRo 的 ACR 设置持久化和 UI 控件系统，实现：
- ACR 作者用一套 C# 代码同时支持 ImGui 悬浮窗和 Web UI
- 设置值通过 `ref` 直接读写，不依赖 label 匹配
- 所有设置统一存储，自动保存

## 2. 设置存储

### 2.1 文件路径
```
{configDir}/setting/ACR/{作者名}/{作者名-职业名}.json
```

### 2.2 文件内容
一个 JSON 文件包含所有设置：ACR 自定义字段 + QT/Hotkey/Overlay 布局属性。

### 2.3 AcrSettings 基类
```csharp
public abstract class AcrSettings
{
    // QT 布局设置（ACR 作者无需手动管理）
    public int QtCols { get; set; }
    public int QtBtnW { get; set; }
    public Dictionary<string, bool> QtVisible { get; set; } = [];
    public Dictionary<string, bool> QtValues { get; set; } = [];

    // 热键布局设置
    public int HkCols { get; set; }
    public int HkBtnSize { get; set; } = 52;
    public Dictionary<string, bool> HkVisible { get; set; } = [];
    public Dictionary<string, string> HkBindings { get; set; } = [];

    // Overlay 尺寸
    public Dictionary<string, int> OverlayContentWidth { get; set; } = [];
    public Dictionary<string, int> OverlayContentHeight { get; set; } = [];

    // Save() 从 ACRLifecycle 读取 author/jobId，不依赖实例字段
    public void Save()
    {
        var author = ACRLifecycle.CurrentAuthor;
        var jobId = ACRLifecycle.CurrentJobId;
        if (string.IsNullOrEmpty(author) || jobId == 0) return;
        SettingMgr.SaveAcrJobSetting(author, jobId, this);
    }
}
```

### 2.4 ACR 作者 Settings 类
```csharp
public class BLM_Setting : AcrSettings
{
    public static BLM_Setting Instance { get; set; } = new();
    public bool test1 = false;   // 用字段，不用属性（ref 要求字段）
    public int test2 = 0;
}
```

### 2.5 序列化
- 使用 `System.Text.Json`
- `IncludeFields = true`（支持字段序列化）
- `WriteIndented = true`
- `Encoder = UnsafeRelaxedJsonEscaping`（中文不转义）
- 用运行时类型序列化：`Serialize(setting, setting.GetType())`

### 2.6 Instance 注入
宿主在 `LoadRotation` 时从文件加载 settings 并注入到 `entry.Settings`。ACR 作者在 `Build` 中将 `Settings` 赋给 `BLM_Setting.Instance`，全链路可用。

## 3. 自动保存

### 3.1 核心原则
**ref 直接读写字段，不需要 ControlValues 中间字典，不需要 SyncSettingsFromControls。**

值通过 ref 直接在控件和 settings 字段之间流动，与 ImGui 原生模式一致。

### 3.2 触发点
| 场景 | 触发方式 |
|------|---------|
| ImGui 控件值变化 | ref 直接写回字段 → `MarkSettingsDirty()` |
| QT 开关切换 | `OnQtChanged()` → 同步 QtValues → `MarkSettingsDirty()` |
| HK 绑定变化 | `MarkSettingsDirty()` |
| 手动保存按钮 | 立即保存 + 清除 dirty 标记 |

### 3.3 防抖机制
```
MarkSettingsDirty()  →  _settingsDirty = true

Update() 每帧调用 CheckAutoSave():
  if (!_settingsDirty) return;
  if (距上次保存 < 1秒) return;
  _settingsDirty = false;
  GetCurrentSettings().Save();  // 写磁盘
```

### 3.4 增量合并
首次加载或 QT/HK 数量变化时，只补充新增项，不覆盖用户已保存的值：
- QtValues：新增 QT 用 `qt.DefaultValue` 补充
- QtVisible：新增 QT 用控件注册时的 `defaultVisible` 补充
- HkVisible：新增 HK 用 `defaultVisible` 补充
- HkBindings：新增 HK 用 `hk.DefaultKey` 补充

## 4. UI 架构（IUiBuilder / IAcrUiBuilder）

### 4.1 两个接口的职责
| 接口 | 使用方 | 渲染模式 | ref |
|------|--------|---------|-----|
| `IUiBuilder` | Trigger 系统 `Draw()` | 只有 Web 编辑器 | 无 |
| `IAcrUiBuilder : IUiBuilder` | ACR `RegisterControls()` | ImGui + Web 双模式 | 有 |

Trigger 系统没有 ImGui UI，只有 Web 编辑器。ACR 同时支持 ImGui 悬浮窗和 Web UI。

### 4.2 IUiBuilder（基础，无 ref）
```csharp
public interface IUiBuilder
{
    void AddTab(string title);
    void EndTab();
    void AddGroup(string title);
    void AddSeparator();
    void AddSameLine();
    void AddMainControl(bool showPause = true, bool showSave = true);
    void AddLabel(string text);

    bool AddCheckbox(string label, bool value);
    bool AddSlider(string label, float min, float max, float value);
    bool AddDropdown(string label, string[] options, string value);
    bool AddIntInput(string label, int value, int step = 1, int stepFast = 10);
    bool AddFloatInput(string label, float value);
    bool AddTextInput(string label, string value);
    bool AddQtToggle(string label, bool value, string? tooltip = null, string? color = null, bool defaultVisible = true);

    void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true);
    void AddTooltip(string targetId, string tooltip);
    void AddHotkeyRow(IHotkeyResolver[] hotkeyIds);
    void AddBuiltinQt(BuiltinQt type, bool? value = null);
}
```

### 4.3 IAcrUiBuilder（ACR 专用，ref 重载）
```csharp
public interface IAcrUiBuilder : IUiBuilder
{
    bool AddCheckbox(string label, ref bool value);
    bool AddSlider(string label, float min, float max, ref float value);
    bool AddDropdown(string label, string[] options, ref string value);
    bool AddIntInput(string label, ref int value, int step = 1, int stepFast = 10);
    bool AddFloatInput(string label, ref float value);
    bool AddTextInput(string label, ref string value);
}
```

### 4.4 UiBuilderImpl 双模式实现
```csharp
public sealed class UiBuilderImpl : IAcrUiBuilder
{
    private readonly bool _isWebMode;

    // Web 模式：存储字段绑定（控件 ID → 字段所属对象 + 字段名）
    internal Dictionary<string, (object Target, string Field)> Bindings { get; } = [];

    // ImGui 模式：ref 重载即时渲染 ImGui 控件，直接读写字段值
    // Web 模式：ref 重载收集控件定义 + 存储绑定关系
}
```

**ImGui 模式（即时渲染）：**
```csharp
public bool AddCheckbox(string label, ref bool value)
{
    var val = value;
    if (ComponentLibrary.Switch(Id, label, ref val))
    {
        value = val;  // ref 直接写回
        return true;  // 通知 ACR 作者值已变化
    }
    return false;
}
```

**Web 模式（收集定义 + 存储绑定）：**
```csharp
public bool AddCheckbox(string label, ref bool value)
{
    var id = "ctrl_" + label;
    _controls.Add(new UiControlDef(id, "checkbox", ...));
    // 通过 CallerArgumentExpression 或调用栈分析获取字段绑定
    StoreBinding(id, ...);
    return false;
}
```

### 4.5 IRotationUI 接口
```csharp
public interface IRotationUI
{
    void RegisterControls(IAcrUiBuilder builder);
}
```

### 4.6 ACRLifecycle 调用方式
- **ImGui 模式**：每帧调用 `RegisterControls`（即时模式），控件直接渲染
- **Web 模式**：首次调用 `RegisterControls`（注册模式），收集定义发送到前端

## 5. ACR 作者使用方式

### 5.1 Settings 类
```csharp
public class BLM_Setting : AcrSettings
{
    public static BLM_Setting Instance { get; set; } = new();
    public bool test1 = false;
    public int test2 = 0;
}
```

### 5.2 Entry 类
```csharp
public class BLM_ACR_Entry : IRotationEntry, ISettingsProvider<BLM_Setting>
{
    public BLM_Setting Settings { get; set; } = new();

    public Rotation? Build(string settingFolder)
    {
        BLM_Setting.Instance = Settings;  // 一行注入，全链路可用
        return new Rotation { ... };
    }

    public IRotationUI? GetRotationUI() => new BLMRotationUI();
}
```

### 5.3 UI 类
```csharp
public class BLMRotationUI : IRotationUI
{
    public void RegisterControls(IAcrUiBuilder builder)
    {
        var s = BLM_Setting.Instance;

        builder.AddTab("设置");
        builder.AddGroup("基础");

        // ref 直接绑定 settings 字段，值变化时自动保存
        if (builder.AddCheckbox("启用功能", ref s.test1))
            s.Save();
        if (builder.AddIntInput("阈值", ref s.test2))
            s.Save();

        // QT 开关（值由 QTHelper 管理，自动保存）
        builder.AddQtToggle("三连", true);
        builder.AddBuiltinQt(BuiltinQt.Burst);

        // 热键
        builder.AddQtHotkey("爆发药", new HotkeyResolver_吃药("爆发药", 49237));
    }
}
```

### 5.4 在其他地方使用
```csharp
// SlotResolver 中
if (BLM_Setting.Instance.test1) { ... }

// EventHandler 中
BLM_Setting.Instance.test2 = 100;
BLM_Setting.Instance.Save();
```

## 6. 移除的机制

以下机制被 ref 直接读写取代，不再需要：
- `ControlValues` 字典（ImGuiOverlayState.ControlValues）
- `SyncControlsFromSettings`（settings → ControlValues）
- `SyncSettingsFromControls`（ControlValues → settings，label 匹配）

## 7. 编译验证

```bash
dotnet build HiAuRo.slnx -c Debug -nologo
```
