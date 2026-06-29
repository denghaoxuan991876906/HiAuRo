using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HiAuRo.ACR;

namespace HiAuRo.Execution;

#region JSON Schema — 触发树格式

/// <summary>执行时间线数据 — 触发树格式</summary>
public sealed class ExecutionTimelineData
{
    /// <summary>时间线名称</summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    /// <summary>副本 ID</summary>
    [JsonPropertyName("TerritoryTypeId")]
    public uint TerritoryId { get; set; }

    /// <summary>备注</summary>
    [JsonPropertyName("Note")]
    public string Note { get; set; } = "";

    /// <summary>唯一标识（新建时自动生成）</summary>
    [JsonPropertyName("Guid")]
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>作者</summary>
    [JsonPropertyName("Author")]
    public string Author { get; set; } = "";

    /// <summary>目标职业（Jobs 枚举值，0=通用）</summary>
    [JsonPropertyName("TargetJob")]
    public int TargetJob { get; set; }

    /// <summary>暴露变量说明（多行文本）</summary>
    [JsonPropertyName("ExposedVarDesc")]
    public string ExposedVarDesc { get; set; } = "";

    /// <summary>暴露的变量名列表</summary>
    [JsonPropertyName("ExposedVars")]
    public List<string> ExposedVarNames { get; set; } = [];

    /// <summary>树根节点数据</summary>
    [JsonPropertyName("TreeRoot")]
    public TriggerNodeData? TreeRoot { get; set; }

    /// <summary>暴露的变量字典</summary>
    [JsonIgnore]
    public Dictionary<string, int> ExposedVars { get; } = [];

    /// <summary>根节点</summary>
    [JsonIgnore]
    public TriggerCompositeNode? Root { get; set; }
}

/// <summary>触发树节点数据 — JSON 反序列化中间格式</summary>
public sealed class TriggerNodeData
{
    /// <summary>节点类型名称（JSON $type）</summary>
    [JsonPropertyName("$type")]
    public string TypeName { get; set; } = "";

    /// <summary>显示名称</summary>
    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = "";

    /// <summary>节点 ID</summary>
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    /// <summary>是否启用</summary>
    [JsonPropertyName("Enable")]
    public bool Enable { get; set; } = true;

    /// <summary>备注</summary>
    [JsonPropertyName("Remark")]
    public string Remark { get; set; } = "";

    /// <summary>标签</summary>
    [JsonPropertyName("Tag")]
    public string Tag { get; set; } = "";

    /// <summary>子节点列表</summary>
    [JsonPropertyName("Childs")]
    public List<TriggerNodeData>? Childs { get; set; }

    /// <summary>是否忽略子节点失败结果</summary>
    [JsonPropertyName("IgnoreNodeResult")]
    public bool? IgnoreNodeResult { get; set; }

    /// <summary>是否任意子节点成功即可</summary>
    [JsonPropertyName("AnyReturn")]
    public bool? AnyReturn { get; set; }

    /// <summary>死亡时是否停止</summary>
    [JsonPropertyName("StopWhenDead")]
    public bool? StopWhenDead { get; set; }

    /// <summary>循环次数</summary>
    [JsonPropertyName("Times")]
    public int? Times { get; set; }

    /// <summary>延迟时间（秒）</summary>
    [JsonPropertyName("Delay")]
    public double? Delay { get; set; }

    /// <summary>条件逻辑类型</summary>
    [JsonPropertyName("CondLogicType")]
    public CondLogicType? CondLogicType { get; set; }

    /// <summary>是否只检查一次</summary>
    [JsonPropertyName("CheckOnce")]
    public bool? CheckOnce { get; set; }

    /// <summary>是否反转条件结果</summary>
    [JsonPropertyName("ReverseResult")]
    public bool? ReverseResult { get; set; }

    /// <summary>触发条件列表</summary>
    [JsonPropertyName("TriggerConds")]
    public List<JsonElement>? TriggerConds { get; set; }

    /// <summary>触发动作列表</summary>
    [JsonPropertyName("TriggerActions")]
    public List<JsonElement>? TriggerActions { get; set; }

    /// <summary>是否仅检查（脚本节点）</summary>
    [JsonPropertyName("OnlyCheck")]
    public bool? OnlyCheck { get; set; }

    /// <summary>脚本内容</summary>
    [JsonPropertyName("Script")]
    public string? Script { get; set; }

    /// <summary>事实轴节点 ID</summary>
    [JsonPropertyName("factNodeId")]
    public string? FactNodeId { get; set; }

    /// <summary>调试信息</summary>
    [JsonPropertyName("Info")]
    public string? Info { get; set; }

    /// <summary>是否仅前置节点</summary>
    [JsonPropertyName("OnlyPreNode")]
    public bool? OnlyPreNode { get; set; }

    /// <summary>转换为 AST 节点</summary>
    public TriggerNode ToNode()
    {
        var type = TypeName.Split(',').FirstOrDefault()?.Split('.').Last() ?? "";

        switch (type)
        {
            case "TreeRoot":
            case "TreeSequence":
                return new TreeSequence
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    IgnoreNodeResult = IgnoreNodeResult ?? (type == "TreeRoot"),
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeParallel":
                return new TreeParallel
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    AnyReturn = AnyReturn ?? false,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeSelect":
                return new TreeSelect
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeLoop":
                return new TreeLoop
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Times = Times ?? 1,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeDelayNode":
                return new TreeDelayNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Delay = Delay ?? 0
                };
            case "TreeCondNode":
                {
                    var conds = new List<ITriggerCond>();
                    if (TriggerConds != null)
                    {
                        foreach (var elem in TriggerConds)
                        {
                            var cond = TriggerConverter.ConvertCondition(elem);
                            if (cond != null) conds.Add(cond);
                        }
                    }
                    return new TreeCondNode
                    {
                        Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                        CondLogicType = CondLogicType ?? Execution.CondLogicType.And,
                        CheckOnce = CheckOnce ?? false,
                        ReverseResult = ReverseResult ?? false,
                        TriggerConds = conds
                    };
                }
            case "TreeActionNode":
                {
                    var actions = new List<ITriggerAction>();
                    if (TriggerActions != null)
                    {
                        foreach (var elem in TriggerActions)
                        {
                            var action = TriggerConverter.ConvertAction(elem);
                            if (action != null) actions.Add(action);
                        }
                    }
                    return new TreeActionNode
                    {
                        Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                        TriggerActions = actions
                    };
                }
            case "TreeScriptNode":
                return new TreeScriptNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    OnlyCheck = OnlyCheck ?? false,
                    Script = Script ?? "",
                    FactNodeId = FactNodeId ?? ""
                };
            case "TreePrintDebugInfoNode":
                return new TreePrintDebugInfoNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Info = Info ?? ""
                };
            case "TreeClearWaitNode":
                return new TreeClearWaitNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    OnlyPreNode = OnlyPreNode ?? true
                };
            default:
                return new TreeSequence
                {
                    Id = Id, DisplayName = $"未知: {type}", Enable = Enable,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
        }
    }
}

#endregion

#region 条件/动作转换

internal static class TriggerConverter
{
    public static ITriggerCond? ConvertCondition(JsonElement elem)
    {
        try
        {
            var typeName = elem.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
            var fullType = typeName.Split(',')[0].Trim();
            if (ExecutionJsonLoader.TryDeserializeCond(fullType, elem, out var cond))
                return cond;
            DService.Instance().Log.Warning($"[ExecAxis] 未知条件类型: {fullType}");
            return null;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ExecAxis] 反序列化条件失败: {ex.Message}");
            return null;
        }
    }

    public static ITriggerAction? ConvertAction(JsonElement elem)
    {
        try
        {
            var typeName = elem.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
            var fullType = typeName.Split(',')[0].Trim();
            if (ExecutionJsonLoader.TryDeserializeAction(fullType, elem, out var action))
                return action;
            DService.Instance().Log.Warning($"[ExecAxis] 未知动作类型: {fullType}");
            return null;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ExecAxis] 反序列化动作失败: {ex.Message}");
            return null;
        }
    }
}

#endregion

#region 内置适配条件/动作

[TriggerDisplay("变量条件", "检查上下文变量值")]
[TriggerTypeName("TriggerCondVariable")]
public sealed class TriggerCond_Variable : ITriggerCond
{
    public string VariableName { get; set; } = "";
    public int VariableVaule { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null) =>
        ExecutionAxis.Instance.Context.GetVariable(VariableName) == VariableVaule;

    public void Draw(HiAuRo.ACR.IUiBuilder builder)
    {
        builder.AddTextInput("VariableName", VariableName);
        builder.AddIntInput("VariableVaule", VariableVaule);
    }
}

[TriggerDisplay("总是真", "始终返回true（调试用）")]
[TriggerTypeName("TriggerCondAlwaysTrue")]
[CloudSync(false)]
public sealed class TriggerCond_AlwaysTrue : ITriggerCond
{
    public string Remark { get; set; } = "";
    public bool Handle(ITriggerCondParams? condParams = null) => true;
    public void Draw(HiAuRo.ACR.IUiBuilder builder) => builder.AddLabel("始终返回 true");
}

[TriggerDisplay("设置变量", "设置上下文变量值")]
[TriggerTypeName("TriggerActionAddVariable")]
public sealed class TriggerAction_SetVariable : ITriggerAction
{
    public string VariableName { get; set; } = "";
    public int SetVariableVaule { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle() { ExecutionAxis.Instance.Context.SetVariable(VariableName, SetVariableVaule); return true; }

    public void Draw(HiAuRo.ACR.IUiBuilder builder)
    {
        builder.AddTextInput("VariableName", VariableName);
        builder.AddIntInput("SetVariableVaule", SetVariableVaule);
    }
}

#endregion

#region 加载器 + 类型注册

/// <summary>执行 JSON 加载器 — 解析触发树 JSON</summary>
public static class ExecutionJsonLoader
{
    private static readonly Dictionary<string, Type> _condTypes = [];
    private static readonly Dictionary<string, Type> _actionTypes = [];
    private static bool _builtInRegistered;

    /// <summary>注册所有内置触发条件/动作类型到 STJ 字典</summary>
    public static void RegisterBuiltInTypes()
    {
        if (_builtInRegistered) return;
        _builtInRegistered = true;
        RebuildFromAssemblies([typeof(ExecutionJsonLoader).Assembly]);
    }

    private static void RegisterType(Type type, Dictionary<string, Type> dict)
    {
        dict.TryAdd(type.FullName!, type);
        dict.TryAdd(type.Name, type);
        var attr = type.GetCustomAttribute<TriggerTypeNameAttribute>();
        if (attr != null)
            dict.TryAdd(attr.TypeDiscriminator, type);
    }

    /// <summary>注册自定义条件类型</summary>
    public static void RegisterConditionType(string fullTypeName, Type type)
    {
        _condTypes[fullTypeName] = type;
    }

    /// <summary>清除已注册的条件/动作类型（插件卸载时调用）</summary>
    public static void Clear()
    {
        _condTypes.Clear();
        _actionTypes.Clear();
        _builtInRegistered = false;
    }

    /// <summary>注册自定义动作类型</summary>
    public static void RegisterActionType(string fullTypeName, Type type)
    {
        _actionTypes[fullTypeName] = type;
    }

    /// <summary>从 Rotation 自动注册所有触发条件/动作（ACR 加载时调用）</summary>
    public static void RegisterFromRotation(Rotation rotation)
    {
        foreach (var cond in rotation.TriggerConditions)
        {
            var t = cond.GetType();
            _condTypes[t.FullName!] = t;
            _condTypes[t.Name] = t;
            // 同时通过 TriggerTypeName 注册，支持 JSON $type 反序列化
            var attr = t.GetCustomAttribute<TriggerTypeNameAttribute>();
            if (attr != null)
                _condTypes[attr.TypeDiscriminator] = t;
        }
        foreach (var action in rotation.TriggerActions)
        {
            var t = action.GetType();
            _actionTypes[t.FullName!] = t;
            _actionTypes[t.Name] = t;
            // 同时通过 TriggerTypeName 注册，支持 JSON $type 反序列化
            var attr = t.GetCustomAttribute<TriggerTypeNameAttribute>();
            if (attr != null)
                _actionTypes[attr.TypeDiscriminator] = t;
        }
        DService.Instance().Log.Information($"[ExecAxis] ACR 注册: {_condTypes.Count} 条件, {_actionTypes.Count} 动作");
    }

    /// <summary>按程序集全量重建触发器类型注册表，保证运行时与 catalog 使用同一来源。</summary>
    public static void RebuildFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        _condTypes.Clear();
        _actionTypes.Clear();

        foreach (var assembly in assemblies.Distinct())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                DService.Instance().Log.Warning(
                    $"[ExecAxis] 程序集 {assembly.GetName().Name} 部分类型加载失败: {ex.LoaderExceptions?.Length ?? 0} 错误");
            }

            foreach (var type in types)
            {
                if (type.IsAbstract) continue;
                if (typeof(ITriggerCond).IsAssignableFrom(type))
                    RegisterType(type, _condTypes);
                else if (typeof(ITriggerAction).IsAssignableFrom(type))
                    RegisterType(type, _actionTypes);
            }
        }

        _builtInRegistered = true;
        DService.Instance().Log.Information($"[ExecAxis] 类型表重建: {_condTypes.Count} 条件, {_actionTypes.Count} 动作");
    }

    internal static bool TryDeserializeCond(string fullTypeName, JsonElement raw, out ITriggerCond? result)
    {
        if (_condTypes.TryGetValue(fullTypeName, out var type))
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            result = JsonSerializer.Deserialize(raw.GetRawText(), type, opts) as ITriggerCond;
            return result != null;
        }
        result = null;
        return false;
    }

    internal static bool TryDeserializeAction(string fullTypeName, JsonElement raw, out ITriggerAction? result)
    {
        if (_actionTypes.TryGetValue(fullTypeName, out var type))
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            result = JsonSerializer.Deserialize(raw.GetRawText(), type, opts) as ITriggerAction;
            return result != null;
        }
        result = null;
        return false;
    }

    /// <summary>从 JSON 字符串解析执行时间线</summary>
    public static ExecutionTimelineData? FromJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<ExecutionTimelineData>(json, options);
            if (data == null) return null;

            if (data.TreeRoot != null)
                data.Root = (TriggerCompositeNode)data.TreeRoot.ToNode();

            foreach (var name in data.ExposedVarNames)
                data.ExposedVars[name] = 0;

            return data;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ExecAxis] JSON 解析失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>从文件加载执行时间线</summary>
    public static ExecutionTimelineData? FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            DService.Instance().Log.Error($"[ExecAxis] 文件不存在: {filePath}");
            return null;
        }
        return FromJson(File.ReadAllText(filePath));
    }
}

#endregion
