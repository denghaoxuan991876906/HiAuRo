using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HiAuRo.Script;

/// <summary>脚本管理器 — 扫描/编译/实例化/生命周期</summary>
public static class ScriptManager
{
    private static readonly List<ScriptRecord> _activeScripts = [];
    private static uint _currentTerritoryId;
    private static bool _combatHooked;
    private static string? _hostDllDir;
    private static bool _dutyHooked;
    private static bool _lastFrameBoundByDuty;

    internal static IReadOnlyList<ScriptRecord> ActiveScripts => _activeScripts;

    /// <summary>上次编译诊断信息（UI 显示用）</summary>
    internal static List<string> CompileDiagnostics { get; } = [];

    private static readonly string[] _allowedPrefixes =
    [
        "System.Runtime", "System.Collections", "System.Linq",
        "System.Numerics", "System.Memory", "System.Primitives",
        "mscorlib", "netstandard",
        "HiAuRo.FA", "HiAuRo", "OmenTools", "Dalamud",
        "FFXIVClientStructs", "ImGui", "Lumina",
    ];

    private static readonly string[] _blockedAssemblies =
    [
        "System.IO.FileSystem", "System.Net",
        "System.Diagnostics.Process", "System.Threading.Tasks.Parallel",
    ];

    /// <summary>设置 HiAuRo.dll 所在目录（从 Plugin.cs 传入）</summary>
    public static void SetHostDllPath(string dllDir) => _hostDllDir = dllDir;

    /// <summary>每帧：检测副本切换、团灭、离本并加载/卸载脚本</summary>
    public static void Update()
    {
        if (!_combatHooked)
        {
            Runtime.CombatContext.StateChanged += OnCombatStateChanged;
            _combatHooked = true;
        }

        if (!_dutyHooked)
        {
            try
            {
                DService.Instance().DutyState.DutyWiped += OnDutyWiped;
            }
            catch { }
            _dutyHooked = true;
        }

        // ResetDutyFlag：检测离开副本（BoundByDuty → false）
        var nowBound = false;
        try { nowBound = DService.Instance().Condition.IsBoundByDuty; }
        catch { }
        if (_lastFrameBoundByDuty && !nowBound)
        {
            DService.Instance().Log.Debug("[ScriptManager] 检测到离开副本");
        }
        _lastFrameBoundByDuty = nowBound;

        var territory = OmenTools.OmenService.GameState.TerritoryType;
        if (territory == 0 || territory == _currentTerritoryId) return;

        _currentTerritoryId = territory;
        UnloadCurrent();
        LoadTerritory(territory);
    }

    /// <summary>团灭时重置所有脚本（等同于战斗重置）</summary>
    private static void OnDutyWiped(Dalamud.Game.DutyState.IDutyStateEventArgs args)
    {
        DService.Instance().Log.Information("[ScriptManager] 团灭，重置脚本");
        ResetAll();
    }

    /// <summary>关闭脚本管理器（Plugin Dispose 时调用）</summary>
    public static void Shutdown()
    {
        if (_combatHooked)
        {
            Runtime.CombatContext.StateChanged -= OnCombatStateChanged;
            _combatHooked = false;
        }
        if (_dutyHooked)
        {
            try
            {
                DService.Instance().DutyState.DutyWiped -= OnDutyWiped;
            }
            catch { }
            _dutyHooked = false;
        }
        UnloadCurrent();
    }

    /// <summary>进战斗时重置所有脚本实例（保留 [UserSetting]）</summary>
    private static void OnCombatStateChanged(Runtime.CombatContext.State oldState, Runtime.CombatContext.State newState)
    {
        if (newState == Runtime.CombatContext.State.InCombat)
            ResetAll();
    }

    /// <summary>重置所有脚本：检测 .cs 文件变更或插件 DLL 重载，必要时重新编译；否则重建实例</summary>
    public static void ResetAll()
    {
        for (var i = 0; i < _activeScripts.Count; i++)
        {
            var record = _activeScripts[i];
            if (record.Type == null) continue;

            try
            {
                // ---- 插件脚本：检测所在程序集是否已重载 ----
                if (record.IsPluginScript)
                {
                    var newType = FindNewTypeInPlugins(record);
                    if (newType != null && newType.Assembly != record.Assembly)
                    {
                        ReloadPluginScript(record, newType);
                        continue;
                    }
                }

                // ---- 普通 .cs 脚本：检测文件是否已修改 ----
                if (!record.IsPluginScript && HasSourceChanged(record))
                {
                    ReloadFromSource(record);
                    continue;
                }

                // ---- 未变更：重建实例（原有逻辑） ----
                var newInstance = Activator.CreateInstance(record.Type);
                if (newInstance == null) continue;

                (record.Instance as IDisposable)?.Dispose();
                ScriptEngine.Instance.UnregisterAll(record);
                record.Instance = newInstance;

                var nameAttr = record.Type.GetCustomAttribute<ScriptTypeAttribute>();
                var name = nameAttr?.Name ?? record.Name;
                LoadSettings(newInstance, record.Type, record.TerritoryId, name);

                ScriptEngine.Instance.Register(record);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[ScriptManager] ResetAll 失败 {record.Name}: {ex.Message}");
            }
        }

        var enableStates = LoadEnableStates();
        LoadParamStates(enableStates, 0);
    }

    /// <summary>检测 .cs 源文件是否已变更</summary>
    private static bool HasSourceChanged(ScriptRecord record)
    {
        if (string.IsNullOrEmpty(record.SourcePath) || !File.Exists(record.SourcePath))
            return false;
        return File.GetLastWriteTimeUtc(record.SourcePath) != record.SourceWriteTimeUtc;
    }

    /// <summary>在已加载的程序集中查找与 record 同名的更新类型（PluginLoader + AppDomain）</summary>
    private static Type? FindNewTypeInPlugins(ScriptRecord record)
    {
        // typeName 可能来自已卸载 ALC，访问 Type 成员会抛异常
        string? typeName = null;
        try { typeName = record.Type?.FullName; }
        catch { }
        if (typeName == null)
        {
            // 后备：从已加载程序集中找同名 ScriptType
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                try
                {
                    var found = asm.GetExportedTypes()
                        .FirstOrDefault(t => t.Name == record.Name && t.GetCustomAttribute<ScriptTypeAttribute>() != null);
                    if (found != null) return found;
                }
                catch { }
            }
        }

        // 先扫 PluginLoader 管理的插件
        foreach (var (_, pluginRecord) in Runtime.PluginLoader.Plugins)
        {
            if (typeName != null)
            {
                try
                {
                    var found = pluginRecord.Assembly.GetExportedTypes()
                        .FirstOrDefault(t => t.FullName == typeName && t.GetCustomAttribute<ScriptTypeAttribute>() != null);
                    if (found != null) return found;
                }
                catch { }
            }
            else
            {
                try
                {
                    var found = pluginRecord.Assembly.GetExportedTypes()
                        .FirstOrDefault(t => t.Name == record.Name && t.GetCustomAttribute<ScriptTypeAttribute>() != null);
                    if (found != null) return found;
                }
                catch { }
            }
        }

        // 再扫 AppDomain
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;
            if (asm == record.Assembly) continue;
            try
            {
                if (typeName != null)
                {
                    var found = asm.GetExportedTypes()
                        .FirstOrDefault(t => t.FullName == typeName && t.GetCustomAttribute<ScriptTypeAttribute>() != null);
                    if (found != null) return found;
                }
                else
                {
                    var found = asm.GetExportedTypes()
                        .FirstOrDefault(t => t.Name == record.Name && t.GetCustomAttribute<ScriptTypeAttribute>() != null);
                    if (found != null) return found;
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>重新编译 .cs 源文件并替换 record</summary>
    private static void ReloadFromSource(ScriptRecord record)
    {
        var code = File.ReadAllText(record.SourcePath);
        var newRecord = CompileSingle(code, record.TerritoryId, record.SourcePath);
        if (newRecord == null) return;

        // 保留之前的开关状态
        newRecord.IsEnabled = record.IsEnabled;
        foreach (var (k, v) in record.MethodEnabled)
            newRecord.MethodEnabled[k] = v;

        // 清理旧资源
        (record.Instance as IDisposable)?.Dispose();
        ScriptEngine.Instance.UnregisterAll(record);
        record.Context?.Unload();

        // 直接替换 record 字段（不修改引用，保持 _activeScripts 不变）
        record.Name = newRecord.Name;
        record.Author = newRecord.Author;
        record.Instance = newRecord.Instance;
        record.Type = newRecord.Type;
        record.Assembly = newRecord.Assembly;
        record.Context = newRecord.Context;
        record.SourceWriteTimeUtc = newRecord.SourceWriteTimeUtc;
        record.CompileError = null;

        ScriptEngine.Instance.Register(record);
        ScriptEngine.Instance.InvokeInit(record);
        DService.Instance().Log.Information($"[ScriptManager] 热重载完成 {record.Name}");
    }

    /// <summary>插件 DLL 重载：用新类型替换 record</summary>
    private static void ReloadPluginScript(ScriptRecord record, Type newType)
    {
        var newInstance = Activator.CreateInstance(newType);
        if (newInstance == null) return;

        var attr = newType.GetCustomAttribute<ScriptTypeAttribute>();
        var name = attr?.Name ?? newType.Name;
        LoadSettings(newInstance, newType, record.TerritoryId, name);

        (record.Instance as IDisposable)?.Dispose();
        ScriptEngine.Instance.UnregisterAll(record);

        record.Instance = newInstance;
        record.Type = newType;
        record.Assembly = newType.Assembly;
        record.CompileError = null;

        ScriptEngine.Instance.Register(record);
        ScriptEngine.Instance.InvokeInit(record);
        DService.Instance().Log.Information($"[ScriptManager] 插件热重载完成 {record.Name}");
    }

    /// <summary>扫描并加载指定副本的脚本（.cs 文件 + 插件 DLL）</summary>
    public static void LoadTerritory(uint territoryId)
    {
        CompileDiagnostics.Clear();
        var enableStates = LoadEnableStates();
        var dir = GetScriptDir();
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*.cs");
            if (files.Length == 0)
                CompileDiagnostics.Add($"目录 {dir} 中存在 0 个 .cs 文件");
            foreach (var file in files)
            {
                try
                {
                    var code = File.ReadAllText(file);
                    CompileAndLoad(code, territoryId, file, enableStates);
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Error($"[ScriptManager] 加载失败 {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        LoadFromPlugins(territoryId, enableStates);
        LoadParamStates(enableStates, territoryId);

        DService.Instance().Log.Information($"[ScriptManager] 副本 {territoryId} 已加载 {_activeScripts.Count} 个脚本");
    }

    /// <summary>从已加载的插件 DLL 中发现 [ScriptType] 类</summary>
    private static void LoadFromPlugins(uint territoryId, Dictionary<string, bool> enableStates)
    {
        var loaded = new HashSet<string>(_activeScripts.Where(s => s.IsPluginScript).Select(s => s.Type?.FullName ?? ""));
        foreach (var (_, record) in Runtime.PluginLoader.Plugins)
        {
            try
            {
                foreach (var type in record.Assembly.GetExportedTypes())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (loaded.Contains(type.FullName)) continue;
                    var attr = type.GetCustomAttribute<ScriptTypeAttribute>();
                    if (attr == null) continue;
                    if (attr.TerritoryIds?.Contains(territoryId) != true) continue;

                    var instance = Activator.CreateInstance(type);
                    if (instance == null) continue;

                    var name = attr.Name ?? type.Name;
                    LoadSettings(instance, type, territoryId, name);

                    var scriptRecord = new ScriptRecord
                    {
                        Name = name,
                        Author = attr.Author,
                        Instance = instance,
                        Type = type,
                        Assembly = record.Assembly,
                        Context = null,
                        SourcePath = $"[Plugin] {type.FullName}",
                        TerritoryId = territoryId,
                        HasSettings = type.GetProperties().Any(p => p.GetCustomAttribute<UserSettingAttribute>() != null),
                        IsPluginScript = true
                    };

                    ApplyEnableStates(scriptRecord, enableStates);

                    ScriptEngine.Instance.Register(scriptRecord);
                    _activeScripts.Add(scriptRecord);
                    DService.Instance().Log.Information($"[ScriptManager] 从插件加载 {name} ({type.Name})");
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[ScriptManager] 扫描插件类型失败: {ex.Message}");
            }
        }
    }

    /// <summary>卸载当前副本所有脚本（插件脚本不卸载其 ALC）</summary>
    public static void UnloadCurrent()
    {
        foreach (var record in _activeScripts)
        {
            try
            {
                (record.Instance as IDisposable)?.Dispose();
                ScriptEngine.Instance.UnregisterAll(record);
            }
            catch { }
        }

        foreach (var record in _activeScripts)
        {
            if (record.IsPluginScript) continue;
            try { record.Context?.Unload(); }
            catch { }
        }

        _activeScripts.Clear();
    }

    /// <summary>编译单个 .cs 文件并加载 [ScriptType] 类</summary>
    private static void CompileAndLoad(string code, uint territoryId, string filePath, Dictionary<string, bool> enableStates)
    {
        var wrapped = PrependDefaultUsings(code);
        var syntaxTree = CSharpSyntaxTree.ParseText(wrapped);
        var references = GetReferences();

        var compilation = CSharpCompilation.Create(
            $"Script_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(5);
            var msg = string.Join("; ", errors.Select(d => d.ToString()));
            DService.Instance().Log.Error($"[ScriptManager] 编译失败 {Path.GetFileName(filePath)}: {msg}");
            return;
        }

        ms.Seek(0, SeekOrigin.Begin);

        var alc = new AssemblyLoadContext($"Script_{Path.GetFileNameWithoutExtension(filePath)}", isCollectible: true);
        alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name);

        var asm = alc.LoadFromStream(ms);

        var found = false;
        foreach (var type in asm.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            var attr = type.GetCustomAttribute<ScriptTypeAttribute>();
            if (attr == null) continue;

            var name = attr.Name ?? type.Name;

            if (attr.TerritoryIds?.Contains(territoryId) != true)
            {
                CompileDiagnostics.Add($"{Path.GetFileName(filePath)}: {name} → 需要副本 {string.Join(",", attr.TerritoryIds ?? [])}，当前 {territoryId}，跳过");
                continue;
            }

            var instance = Activator.CreateInstance(type);
            if (instance == null) continue;

            LoadSettings(instance, type, territoryId, name);

            var record = new ScriptRecord
            {
                Name = name,
                Author = attr.Author,
                Instance = instance,
                Type = type,
                Assembly = asm,
                Context = alc,
                SourcePath = filePath,
                TerritoryId = territoryId,
                HasSettings = type.GetProperties().Any(p => p.GetCustomAttribute<UserSettingAttribute>() != null)
            };

            ApplyEnableStates(record, enableStates);

            ScriptEngine.Instance.Register(record);
            _activeScripts.Add(record);
            found = true;
            DService.Instance().Log.Information($"[ScriptManager] 已加载 {name} ({type.Name})");
            return;
        }

        if (!found)
        {
            var typeNames = string.Join(", ", asm.GetTypes().Select(t => t.Name));
            DService.Instance().Log.Warning($"[ScriptManager] {Path.GetFileName(filePath)} 编译成功，但无匹配当前的副本 {territoryId} 的 [ScriptType] 类。找到的类型: {typeNames}");
        }

        alc.Unload();
    }

    /// <summary>在用户代码前注入默认 using（与 GlobalUsings.cs 对齐）</summary>
    private static string PrependDefaultUsings(string userCode)
    {
        var usings =
"""
using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Drawing;
using HiAuRo;
using HiAuRo.ACR;
using HiAuRo.Script;
using HiAuRo.Execution;
using HiAuRo.Execution.Events;
using HiAuRo.Infrastructure;
using HiAuRo.Rendering;
using HiAuRo.Vfx;
using OmenTools;
using OmenTools.ImGuiOm;
using OmenTools.Extensions;
using OmenTools.OmenService;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImGuizmo;
using Dalamud.Bindings.ImPlot;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using static OmenTools.Global.Globals;
using static OmenTools.Info.Game.Data.Addons;

""";

        if (IsFaPluginLoaded())
            usings +=
"""
using HiAuRo.FA.Utils;
using HiAuRo.FA.Shapes;

""";

        return usings + userCode;
    }

    private static bool IsFaPluginLoaded()
    {
        try
        {
            if (AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "HiAuRo.FA"))
                return true;

            foreach (var (_, record) in Runtime.PluginLoader.Plugins)
            {
                if (record.Assembly.GetName().Name == "HiAuRo.FA")
                    return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>编译单个 .cs 文件并返回 ScriptRecord（不注册/不加入 _activeScripts）</summary>
    private static ScriptRecord? CompileSingle(string code, uint territoryId, string filePath)
    {
        var wrapped = PrependDefaultUsings(code);
        var syntaxTree = CSharpSyntaxTree.ParseText(wrapped);
        var references = GetReferences();

        var compilation = CSharpCompilation.Create(
            $"Script_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(5);
            var msg = string.Join("; ", errors.Select(d => d.ToString()));
            DService.Instance().Log.Error($"[ScriptManager] 编译失败 {Path.GetFileName(filePath)}: {msg}");
            return null;
        }
        if (ms.Length == 0)
        {
            DService.Instance().Log.Warning($"[ScriptManager] 编译结果为空 {Path.GetFileName(filePath)}");
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);

        var alc = new AssemblyLoadContext($"Script_{Path.GetFileNameWithoutExtension(filePath)}", isCollectible: true);
        alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name);

        var asm = alc.LoadFromStream(ms);

        foreach (var type in asm.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            var attr = type.GetCustomAttribute<ScriptTypeAttribute>();
            if (attr == null) continue;
            if (attr.TerritoryIds?.Contains(territoryId) != true) continue;

            var instance = Activator.CreateInstance(type);
            if (instance == null) continue;

            var name = attr.Name ?? type.Name;
            LoadSettings(instance, type, territoryId, name);

            return new ScriptRecord
            {
                Name = name,
                Author = attr.Author,
                Instance = instance,
                SourceWriteTimeUtc = File.GetLastWriteTimeUtc(filePath),
                Type = type,
                Assembly = asm,
                Context = alc,
                SourcePath = filePath,
                TerritoryId = territoryId,
                HasSettings = type.GetProperties().Any(p => p.GetCustomAttribute<UserSettingAttribute>() != null)
            };
        }

        alc.Unload();
        return null;
    }

    private static void ApplyEnableStates(ScriptRecord record, Dictionary<string, bool> states)
    {
        if (states.TryGetValue(record.Name, out var en))
            record.IsEnabled = en;

        var prefix = record.Name + "::";
        foreach (var (key, val) in states)
        {
            if (key.StartsWith(prefix))
                record.MethodEnabled[key[prefix.Length..]] = val;
        }
    }

    /// <summary>从磁盘加载 [UserSetting] 持久化值到脚本实例</summary>
    private static void LoadSettings(object instance, Type type, uint territoryId, string scriptName)
    {
        var settingsPath = GetSettingsPath(territoryId, scriptName);
        if (!File.Exists(settingsPath)) return;

        try
        {
            var json = File.ReadAllText(settingsPath);
            var saved = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
            if (saved == null) return;

            foreach (var (key, elem) in saved)
            {
                var prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite) continue;
                try
                {
                    var val = System.Text.Json.JsonSerializer.Deserialize(elem.GetRawText(), prop.PropertyType);
                    prop.SetValue(instance, val);
                }
                catch { }
            }
        }
        catch { }
    }

    /// <summary>保存脚本的 [UserSetting] 到磁盘</summary>
    public static void SaveSettings(ScriptRecord record)
    {
        var type = record.Type;
        if (type == null) return;

        var settings = new Dictionary<string, object?>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<UserSettingAttribute>();
            if (attr == null) continue;
            if (!prop.CanRead) continue;
            settings[prop.Name] = prop.GetValue(record.Instance);
        }
        if (settings.Count == 0) return;

        var settingsPath = GetSettingsPath(record.TerritoryId, record.Name);
        var dir = Path.GetDirectoryName(settingsPath);
        if (dir != null) Directory.CreateDirectory(dir);

        var json = System.Text.Json.JsonSerializer.Serialize(settings,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
    }

    private static string GetScriptDir()
        => Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "Scripts");

    internal static string GetSettingsPath(uint territoryId, string scriptName)
        => Path.Combine(GetScriptDir(), $"{scriptName}_settings.json");

    private static string GetEnableStatePath()
        => Path.Combine(GetScriptDir(), "scripts_enable.json");

    private static Dictionary<string, bool> LoadEnableStates()
    {
        var path = GetEnableStatePath();
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? [];
        }
        catch { return []; }
    }

    internal static void SaveEnableStates()
    {
        var states = new Dictionary<string, bool>();
        foreach (var r in _activeScripts)
        {
            states[r.Name] = r.IsEnabled;
            foreach (var (key, en) in r.MethodEnabled)
                states[$"{r.Name}::{key}"] = en;
        }

        var path = GetEnableStatePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        var json = System.Text.Json.JsonSerializer.Serialize(states,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string GetParamStatePath()
        => Path.Combine(GetScriptDir(), "scripts_params.json");

    private static void LoadParamStates(Dictionary<string, bool> enableStates, uint territoryId)
    {
        var path = GetParamStatePath();
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
            if (raw == null) return;

            foreach (var r in _activeScripts)
            {
                foreach (var (methodKey, fieldNames) in r.MethodParamNames)
                {
                    foreach (var fieldName in fieldNames)
                    {
                        var stateKey = $"{r.Name}::{methodKey}::{fieldName}";
                        if (!raw.TryGetValue(stateKey, out var elem)) continue;

                        var field = r.Type?.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                        if (field != null && r.Instance != null)
                        {
                            try
                            {
                                var val = System.Text.Json.JsonSerializer.Deserialize(elem.GetRawText(), field.FieldType);
                                if (val != null) field.SetValue(r.Instance, val);
                            }
                            catch { }
                            continue;
                        }

                        var prop = r.Type?.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null && prop.CanWrite && r.Instance != null)
                        {
                            try
                            {
                                var val = System.Text.Json.JsonSerializer.Deserialize(elem.GetRawText(), prop.PropertyType);
                                if (val != null) prop.SetValue(r.Instance, val);
                            }
                            catch { }
                        }
                    }
                }
            }
        }
        catch { }
    }

    internal static void SaveParamStates()
    {
        var states = new Dictionary<string, object?>();
        foreach (var r in _activeScripts)
        {
            foreach (var (methodKey, fieldNames) in r.MethodParamNames)
            {
                foreach (var fieldName in fieldNames)
                {
                    var stateKey = $"{r.Name}::{methodKey}::{fieldName}";
                    var field = r.Type?.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null && r.Instance != null)
                    {
                        states[stateKey] = field.GetValue(r.Instance);
                        continue;
                    }
                    var prop = r.Type?.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanRead && r.Instance != null)
                        states[stateKey] = prop.GetValue(r.Instance);
                }
            }
        }
        if (states.Count == 0) return;

        var path = GetParamStatePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        var json = System.Text.Json.JsonSerializer.Serialize(states,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>收集编译引用（白名单过滤）</summary>
    private static MetadataReference[] GetReferences()
    {
        var refs = new List<MetadataReference>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;
            var name = asm.GetName().Name ?? "";
            if (!IsAllowed(name)) continue;

            // Location 正常 → 直接引用
            if (!string.IsNullOrEmpty(asm.Location))
            {
                try { refs.Add(MetadataReference.CreateFromFile(asm.Location)); continue; }
                catch { }
            }

            // Location 为空（Dalamud LoadFromStream）→ 从 host DLL 目录找
            if (_hostDllDir != null)
            {
                var path = Path.Combine(_hostDllDir, name + ".dll");
                if (File.Exists(path))
                {
                    try { refs.Add(MetadataReference.CreateFromFile(path)); continue; }
                    catch { }
                }
            }
        }

        foreach (var (_, record) in Runtime.PluginLoader.Plugins)
        {
            try { refs.Add(MetadataReference.CreateFromImage(record.DllBytes)); }
            catch { }
        }

        // 确保核心引用存在
        EnsureRef(refs, typeof(object).Assembly, "System.Runtime");
        EnsureRef(refs, typeof(object).Assembly, "System.Private.CoreLib");
        EnsureRef(refs, typeof(List<>).Assembly, "System.Collections");
        EnsureRef(refs, typeof(System.Linq.Enumerable).Assembly, "System.Linq");
        EnsureRef(refs, typeof(System.Numerics.Vector3).Assembly, "System.Numerics.Vectors");

        // 强制加入 HiAuRo + OmenTools 元数据引用
        // 不依赖 auto-scan 的 Location（Dalamud 环境下可能为空）
        ForceAddAssembly(refs, typeof(ScriptManager).Assembly);
        ForceAddAssembly(refs, typeof(OmenTools.OmenService.GameState).Assembly);

        return refs.ToArray();
    }

    private static void ForceAddAssembly(List<MetadataReference> refs, Assembly asm)
    {
        var name = asm.GetName().Name;
        if (name == null || refs.Any(r => r.Display?.Contains(name) == true)) return;

        // Try file path first
        if (!string.IsNullOrEmpty(asm.Location))
        {
            try { refs.Add(MetadataReference.CreateFromFile(asm.Location)); return; }
            catch { }
        }

        // Try host DLL directory (Dalamud LoadFromStream → Location 为空)
        if (_hostDllDir != null)
        {
            var path = Path.Combine(_hostDllDir, name + ".dll");
            if (File.Exists(path))
            {
                try { refs.Add(MetadataReference.CreateFromFile(path)); return; }
                catch { }
            }
        }

        // Try CodeBase (for shadow-copied assemblies)
        var codeBase = asm.GetName().CodeBase;
        if (!string.IsNullOrEmpty(codeBase) && codeBase.StartsWith("file:///"))
        {
            var path = codeBase["file:///".Length..];
            path = Uri.UnescapeDataString(path);
            try { refs.Add(MetadataReference.CreateFromFile(path)); return; }
            catch { }
        }

        // Fallback: scan modules
        foreach (var module in asm.Modules)
        {
            if (!string.IsNullOrEmpty(module.Name) && module.Name.EndsWith(".dll"))
            {
                try
                {
                    var fullPath = Path.Combine(Path.GetDirectoryName(asm.Location) ?? ".", module.Name);
                    if (File.Exists(fullPath))
                    {
                        refs.Add(MetadataReference.CreateFromFile(fullPath));
                        return;
                    }
                }
                catch { }
            }
        }

        DService.Instance().Log.Warning($"[ScriptManager] 无法加载程序集引用: {name}");
    }

    private static void EnsureRef(List<MetadataReference> refs, Assembly asm, string name)
    {
        if (refs.Any(r => r.Display?.Contains(name) == true)) return;
        if (asm != null && !string.IsNullOrEmpty(asm.Location))
        {
            try { refs.Add(MetadataReference.CreateFromFile(asm.Location)); }
            catch { }
        }
    }

    private static bool IsAllowed(string name)
    {
        foreach (var blocked in _blockedAssemblies)
        {
            if (name.StartsWith(blocked, StringComparison.OrdinalIgnoreCase)) return false;
        }

        foreach (var prefix in _allowedPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>ALC 程序集解析</summary>
    private static Assembly? ResolveAssembly(AssemblyLoadContext ctx, AssemblyName name)
    {
        if (_allowedPrefixes.Any(p => name.Name?.StartsWith(p) == true))
        {
            if (name.Name == "HiAuRo")
                return typeof(ScriptManager).Assembly;

            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (asm.GetName().Name == name.Name)
                    return asm;
            }

            // 从 PluginLoader 加载的程序集中查找（如 HiAuRo.FA）
            foreach (var (_, record) in Runtime.PluginLoader.Plugins)
            {
                if (record.Assembly.GetName().Name == name.Name)
                    return record.Assembly;
            }
        }

        var scriptsDir = GetScriptDir();
        var dllPath = Path.Combine(scriptsDir, name.Name + ".dll");
        if (File.Exists(dllPath))
        {
            try { return ctx.LoadFromAssemblyPath(dllPath); }
            catch { }
        }

        return null;
    }
}

public sealed class ScriptRecord : IDisposable
{
    public string Name { get; set; } = "";
    public string? Author { get; set; }
    public object? Instance { get; set; }
    public Type? Type { get; set; }
    public Assembly? Assembly { get; set; }
    public AssemblyLoadContext? Context { get; set; }
    public string SourcePath { get; set; } = "";
    public uint TerritoryId { get; set; }
    public string? CompileError { get; set; }
    public bool HasSettings { get; set; }
    public bool IsPluginScript { get; set; }
    public DateTime SourceWriteTimeUtc { get; set; }

    /// <summary>脚本是否启用（UI 控制）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>方法级开关：key → enabled（由 ScriptEngine.Register 填默认值）</summary>
    public Dictionary<string, bool> MethodEnabled { get; } = [];

    /// <summary>方法参数列表：methodKey → [字段名, ...]（由 ScriptEngine.Register 填入）</summary>
    public Dictionary<string, List<string>> MethodParamNames { get; } = [];

    public void Dispose()
    {
        if (Instance is IDisposable d) d.Dispose();
    }
}

/// <summary>脚本系统全局开关</summary>
public static class ScriptGlobal
{
    /// <summary>是否启用脚本系统</summary>
    public static bool Enabled { get; set; } = true;
}