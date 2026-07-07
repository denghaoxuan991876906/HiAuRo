using System.Reflection;
using HiAuRo.ACR;
using System.Text.Json.Serialization;
using System.Collections;

namespace HiAuRo.Execution;

/// <summary>
/// 触发器显示属性 - 用于指定中文名称和描述
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TriggerDisplayAttribute : Attribute
{
    /// <summary>显示名称</summary>
    public string DisplayName { get; }
    /// <summary>描述</summary>
    public string Description { get; }

    /// <summary>初始化触发器显示属性</summary>
    public TriggerDisplayAttribute(string displayName, string description = "")
    {
        DisplayName = displayName;
        Description = description;
    }
}

/// <summary>触发器类型鉴别符 - JSON $type 字段值</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TriggerTypeNameAttribute(string typeDiscriminator) : Attribute
{
    /// <summary>类型鉴别符</summary>
    public string TypeDiscriminator { get; } = typeDiscriminator;
}

/// <summary>
/// 云同步属性 - 标记触发器是否同步到GitHub云端
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CloudSyncAttribute : Attribute
{
    /// <summary>是否启用云同步</summary>
    public bool CloudSync { get; }

    /// <summary>初始化云同步属性</summary>
    public CloudSyncAttribute(bool cloudSync = true)
    {
        CloudSync = cloudSync;
    }
}

/// <summary>
/// 触发器参数信息
/// </summary>
public sealed class ParameterInfo
{
    /// <summary>参数名</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>参数类型 ("number"|"text"|"checkbox"|"select")</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>参数描述</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>默认值</summary>
    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; init; }

    /// <summary>枚举值列表</summary>
    [JsonPropertyName("enumValues")]
    public List<string>? EnumValues { get; init; }
}

/// <summary>
/// 触发器信息
/// </summary>
public sealed class TriggerInfo
{
    /// <summary>C# 全名</summary>
    [JsonPropertyName("typeName")]
    public string TypeName { get; init; } = string.Empty;

    /// <summary>JSON $type 字段值</summary>
    [JsonPropertyName("typeDiscriminator")]
    public string TypeDiscriminator { get; init; } = string.Empty;

    /// <summary>中文显示名称</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>描述</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>类别 ("builtin"|"acr"|"plugin"|"local")</summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>是否启用云同步</summary>
    [JsonPropertyName("cloudSync")]
    public bool CloudSync { get; init; }

    /// <summary>参数列表</summary>
    [JsonPropertyName("parameters")]
    public List<ParameterInfo> Parameters { get; init; } = [];

    /// <summary>UI 控件列表（来自 Draw() 的声明）</summary>
    [JsonPropertyName("controls")]
    public List<object>? Controls { get; init; }
}

/// <summary>
/// 触发器目录 - 包含条件和动作两类触发器
/// </summary>
public sealed class TriggerCatalog
{
    /// <summary>条件触发器列表</summary>
    [JsonPropertyName("conditions")]
    public List<TriggerInfo> Conditions { get; init; } = [];

    /// <summary>动作触发器列表</summary>
    [JsonPropertyName("actions")]
    public List<TriggerInfo> Actions { get; init; } = [];

    /// <summary>脚本触发器列表</summary>
    [JsonPropertyName("scripts")]
    public List<TriggerInfo> Scripts { get; init; } = [];

    /// <summary>添加条件触发器</summary>
    public void AddCondition(TriggerInfo info) => Conditions.Add(info);
    /// <summary>添加动作触发器</summary>
    public void AddAction(TriggerInfo info) => Actions.Add(info);
    /// <summary>添加脚本触发器</summary>
    public void AddScript(TriggerInfo info) => Scripts.Add(info);
}

/// <summary>
/// 触发器目录构建器 - 通过反射扫描程序集生成触发器元数据
/// </summary>
public static class TriggerCatalogBuilder
{
    /// <summary>
    /// 从程序集构建触发器目录
    /// </summary>
    public static TriggerCatalog BuildFromAssembly(Assembly assembly, string category)
    {
        var catalog = new TriggerCatalog();

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
            DService.Instance().Log.Warning($"[TriggerCatalog] 程序集 {assembly.GetName().Name} 部分类型加载失败: {ex.LoaderExceptions?.Length ?? 0} 错误");
        }

        var triggerTypes = types.Where(t => t is { IsAbstract: false, IsInterface: false }
                                               && typeof(ITriggerBase).IsAssignableFrom(t));

        foreach (var type in triggerTypes)
        {
            var info = BuildTriggerInfo(type, category);
            if (typeof(ITriggerCond).IsAssignableFrom(type))
                catalog.AddCondition(info);
            else if (typeof(ITriggerAction).IsAssignableFrom(type))
                catalog.AddAction(info);
            else if (typeof(ITriggerScript).IsAssignableFrom(type))
                catalog.AddScript(info);
        }

        return catalog;
    }

    /// <summary>合并目录（所有列表直接追加，不处理去重）</summary>
    public static void MergeInto(TriggerCatalog target, TriggerCatalog source)
    {
        target.Conditions.AddRange(source.Conditions);
        target.Actions.AddRange(source.Actions);
        target.Scripts.AddRange(source.Scripts);
    }

    /// <summary>
    /// 从显式提供的类型列表构建触发器目录
    /// </summary>
    public static TriggerCatalog BuildFromTypes(
        IEnumerable<Type> condTypes,
        IEnumerable<Type> actionTypes,
        string category)
    {
        var catalog = new TriggerCatalog();

        foreach (var type in condTypes)
        {
            if (type is { IsAbstract: false, IsInterface: false }
                && typeof(ITriggerCond).IsAssignableFrom(type))
            {
                catalog.AddCondition(BuildTriggerInfo(type, category));
            }
        }

        foreach (var type in actionTypes)
        {
            if (type is { IsAbstract: false, IsInterface: false }
                && typeof(ITriggerAction).IsAssignableFrom(type))
            {
                catalog.AddAction(BuildTriggerInfo(type, category));
            }
        }

        return catalog;
    }

    /// <summary>
    /// 从类型构建触发器信息
    /// </summary>
    private static TriggerInfo BuildTriggerInfo(Type type, string category)
    {
        var typeNameAttr = type.GetCustomAttribute<TriggerTypeNameAttribute>();
        var typeDiscriminator = typeNameAttr?.TypeDiscriminator
                          ?? $"{type.FullName}, {type.Assembly.GetName().Name}";

        var displayAttr = type.GetCustomAttribute<TriggerDisplayAttribute>();
        string displayName;
        string? description;

        if (displayAttr != null)
        {
            displayName = displayAttr.DisplayName;
            description = displayAttr.Description;
        }
        else
        {
            // 从类名派生显示名称
            displayName = DeriveDisplayNameFromClassName(type.Name);
            description = null;
        }

        // 获取云同步设置
        var cloudSyncAttr = type.GetCustomAttribute<CloudSyncAttribute>();
        var cloudSync = cloudSyncAttr?.CloudSync ?? true;

        // 反射构造函数参数
        var parameters = ExtractParameters(type);

        // 通过 Draw() 收集 UI 控件声明
        List<object>? controls = null;
        if (DService.IsInitialized)
        {
            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance != null)
                    controls = TryExtractControls(instance);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[TriggerMetadata] Draw() 调用失败 ({type.Name}): {ex.Message}");
            }
        }

        return new TriggerInfo
        {
            TypeName = type.FullName ?? type.Name,
            TypeDiscriminator = typeDiscriminator,
            DisplayName = displayName,
            Description = description,
            Category = ResolveTriggerCategory(type, category, displayName),
            CloudSync = cloudSync,
            Parameters = parameters,
            Controls = controls
        };
    }

    private static string ResolveTriggerCategory(Type type, string category, string displayName)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "未分类";
        if (category.Contains('\\'))
            return category;
        if (!string.Equals(category, "builtin", StringComparison.OrdinalIgnoreCase))
            return category;

        var key = displayName + type.Name;
        if (typeof(ITriggerCond).IsAssignableFrom(type))
        {
            if (ContainsAny(key, "变量")) return @"内置\变量";
            if (ContainsAny(key, "倒计时", "经过时间", "上次技能", "总是真")) return @"内置\时间";
            if (ContainsAny(key, "技能冷却", "技能后", "收到技能效果", "无目标技能效果", "敌人读条")) return @"内置\技能";
            if (ContainsAny(key, "单位", "Actor", "目标", "角色类型", "职能")) return @"内置\单位与目标";
            if (ContainsAny(key, "连线", "图标", "Omega")) return @"内置\机制";
            if (ContainsAny(key, "地图", "天气", "日志")) return @"内置\环境";
            return @"内置\条件";
        }

        if (typeof(ITriggerAction).IsAssignableFrom(type))
        {
            if (ContainsAny(key, "变量")) return @"内置\变量";
            if (ContainsAny(key, "TP", "移动")) return @"内置\移动";
            if (ContainsAny(key, "技能", "吃药", "Slot")) return @"内置\技能";
            if (ContainsAny(key, "切换", "Rotation", "起手", "停手", "目标", "自动攻击")) return @"内置\战斗控制";
            if (ContainsAny(key, "命令", "按键")) return @"内置\系统";
            return @"内置\动作";
        }

        return @"内置\脚本";
    }

    private static bool ContainsAny(string value, params string[] needles)
        => needles.Any(value.Contains);

    /// <summary>
    /// 从类名派生显示名称
    /// </summary>
    private static string DeriveDisplayNameFromClassName(string className)
    {
        // 移除常见前缀
        var prefixes = new[] { "TriggerCond_", "TriggerAction_", "TriggerCond", "TriggerAction" };

        foreach (var prefix in prefixes)
        {
            if (className.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return className.Substring(prefix.Length);
            }
        }

        return className;
    }

    /// <summary>
    /// 提取类型的构造函数参数信息
    /// </summary>
    private static List<ParameterInfo> ExtractParameters(Type type)
    {
        var parameters = new List<ParameterInfo>();

        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
        {
            return parameters;
        }

        // 使用第一个构造函数
        var constructor = constructors[0];
        var ctorParams = constructor.GetParameters();

        foreach (var param in ctorParams)
        {
            var paramType = param.ParameterType;
            var paramInfo = new ParameterInfo
            {
                Name = param.Name!,
                Type = MapParameterType(paramType),
                Description = null,
                DefaultValue = param.HasDefaultValue ? param.DefaultValue : null,
                EnumValues = paramType.IsEnum ? Enum.GetNames(paramType).ToList() : null
            };

            parameters.Add(paramInfo);
        }

        return parameters;
    }

    /// <summary>
    /// 将CLR类型映射为参数类型字符串
    /// </summary>
    private static string MapParameterType(Type type)
    {
        // 处理Nullable类型
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            // Nullable<T> - 使用底层类型映射
            type = underlyingType;
        }

        // 枚举类型
        if (type.IsEnum)
        {
            return "select";
        }

        // 数字类型
        if (type == typeof(uint)
            || type == typeof(int)
            || type == typeof(byte)
            || type == typeof(long)
            || type == typeof(short)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(sbyte)
            || type == typeof(ushort)
            || type == typeof(ulong))
        {
            return "number";
        }

        // 字符串类型
        if (type == typeof(string))
        {
            return "text";
        }

        // 布尔类型
        if (type == typeof(bool))
        {
            return "checkbox";
        }

        // 默认返回text
        return "text";
    }

    private static List<object>? TryExtractControls(object instance)
    {
        // ponytail: controls 只是编辑器增强信息，离开宿主依赖环境时拿不到就跳过，不影响 catalog 主体。
        var builderType = typeof(TriggerCatalogBuilder).Assembly.GetType("HiAuRo.UI.UiBuilderImpl");
        if (builderType == null) return null;

        var builderObj = Activator.CreateInstance(builderType);
        if (builderObj is not HiAuRo.ACR.IUiBuilder builder) return null;

        switch (instance)
        {
            case ITriggerCond condInstance:
                condInstance.Draw(builder);
                break;
            case ITriggerAction actionInstance:
                actionInstance.Draw(builder);
                break;
            default:
                return null;
        }

        if (builderType.GetMethod("GetControls")?.Invoke(builderObj, null) is not IEnumerable controls)
            return null;

        var result = new List<object>();
        foreach (var control in controls)
        {
            var controlType = control.GetType();
            result.Add(new
            {
                Id = controlType.GetProperty("Id")?.GetValue(control),
                Type = controlType.GetProperty("Type")?.GetValue(control),
                ParentId = controlType.GetProperty("ParentId")?.GetValue(control),
                Label = controlType.GetProperty("Label")?.GetValue(control),
                defaultValue = controlType.GetProperty("Value")?.GetValue(control),
                Options = controlType.GetProperty("Options")?.GetValue(control),
                Meta = controlType.GetProperty("Meta")?.GetValue(control)
            });
        }

        return result;
    }
}
