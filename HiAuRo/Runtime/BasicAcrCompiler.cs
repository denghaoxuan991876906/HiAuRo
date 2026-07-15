using System.Runtime.Loader;
using HiAuRo.ACR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HiAuRo.Runtime;

internal sealed record BasicAcrDiagnostic(
    string Message,
    string? FilePath,
    int? Line,
    int? Column)
{
    public override string ToString()
    {
        if (string.IsNullOrEmpty(FilePath) || Line is null || Column is null)
            return Message;

        return $"{Path.GetFileName(FilePath)}({Line},{Column}): {Message}";
    }
}

internal sealed class BasicAcrCompilation : IDisposable
{
    private AssemblyLoadContext? _loadContext;
    private BasicAcrEntry? _entry;

    internal BasicAcrCompilation(
        AssemblyLoadContext loadContext,
        BasicAcrEntry entry,
        Jobs targetJob,
        string scriptTypeName)
    {
        _loadContext = loadContext;
        _entry = entry;
        TargetJob = targetJob;
        ScriptTypeName = scriptTypeName;
    }

    internal BasicAcrEntry Entry => Volatile.Read(ref _entry)
        ?? throw new ObjectDisposedException(nameof(BasicAcrCompilation));

    internal Jobs TargetJob { get; }

    internal string ScriptTypeName { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _entry, null);
        var loadContext = Interlocked.Exchange(ref _loadContext, null);
        loadContext?.Unload();
    }
}

internal sealed class BasicAcrCompileResult : IDisposable
{
    private BasicAcrCompilation? _compilation;

    internal BasicAcrCompileResult(
        BasicAcrCompilation? compilation,
        IReadOnlyList<BasicAcrDiagnostic> diagnostics,
        string? errorMessage)
    {
        _compilation = compilation;
        Diagnostics = diagnostics;
        ErrorMessage = errorMessage;
        Success = compilation is not null;
    }

    internal BasicAcrCompilation? Compilation => _compilation;

    internal IReadOnlyList<BasicAcrDiagnostic> Diagnostics { get; }

    internal string? ErrorMessage { get; }

    internal bool Success { get; }

    internal BasicAcrCompilation? TakeCompilation() =>
        Interlocked.Exchange(ref _compilation, null);

    public void Dispose()
    {
        Interlocked.Exchange(ref _compilation, null)?.Dispose();
    }
}

internal static class BasicAcrCompiler
{
    private static readonly string[] AllowedAssemblyPrefixes =
    [
        "System.",
        "Microsoft.CSharp",
        "mscorlib",
        "netstandard",
        "OmenTools",
        "Dalamud",
        "FFXIVClientStructs",
        "Lumina",
        "ImGuiNET",
        "TerraFX",
    ];

    internal static BasicAcrCompileResult Compile(
        string source,
        string sourcePath)
    {
        IReadOnlyList<BasicAcrDiagnostic> diagnostics = [];
        AssemblyLoadContext? candidateContext = null;

        try
        {
            var bindings = SharedAssemblyResolver.CaptureHost();
            var syntaxTree = CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(LanguageVersion.Latest),
                sourcePath);
            var compilation = CSharpCompilation.Create(
                $"HiAuRo.BasicAcr.{Guid.NewGuid():N}",
                [syntaxTree],
                bindings.GetMetadataReferences(IsAllowedAssembly, includeExtensions: false),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release));

            using var assemblyStream = new MemoryStream();
            var emitResult = compilation.Emit(assemblyStream);
            diagnostics = emitResult.Diagnostics
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
                .Select(ToBasicDiagnostic)
                .ToArray();

            if (!emitResult.Success)
            {
                var errorMessage = emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(ToBasicDiagnostic)
                    .FirstOrDefault()?.ToString()
                    ?? "Basic ACR 编译失败";
                return new BasicAcrCompileResult(null, diagnostics, errorMessage);
            }

            assemblyStream.Position = 0;
            candidateContext = new AssemblyLoadContext(
                $"HiAuRo.BasicAcr.{Guid.NewGuid():N}",
                isCollectible: true);
            candidateContext.Resolving += (_, requestedName) => bindings.Resolve(requestedName);
            var assembly = candidateContext.LoadFromStream(assemblyStream);

            var scriptTypes = assembly.GetExportedTypes()
                .Where(type => !type.IsAbstract && typeof(IBasicAcrScript).IsAssignableFrom(type))
                .ToArray();
            if (scriptTypes.Length != 1)
                throw new InvalidOperationException("Basic ACR 必须包含且仅包含一个 public 非抽象 IBasicAcrScript 实现");

            var scriptType = scriptTypes[0];
            if (scriptType.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException("Basic ACR 入口必须具有 public 无参构造函数");

            var script = (IBasicAcrScript)Activator.CreateInstance(scriptType)!;
            var targetJob = script.TargetJob;
            if (targetJob == Jobs.None || !Enum.IsDefined(typeof(Jobs), targetJob))
                throw new InvalidOperationException($"Basic ACR TargetJob 无效: {(int)targetJob}");

            var resolvers = script.BuildSlotResolvers()?.ToList()
                ?? throw new InvalidOperationException("Basic ACR BuildSlotResolvers 不得返回 null");
            if (resolvers.Count == 0)
                throw new InvalidOperationException("Basic ACR BuildSlotResolvers 不得返回空集合");
            if (resolvers.Any(resolver => resolver is null))
                throw new InvalidOperationException("Basic ACR SlotResolverData 不得为 null");
            if (resolvers.Any(resolver => resolver.Resolver is null))
                throw new InvalidOperationException("Basic ACR Resolver 不得为 null");

            var scriptTypeName = scriptType.Name;
            var entry = new BasicAcrEntry(scriptTypeName, targetJob, resolvers);
            var loadedCompilation = new BasicAcrCompilation(
                candidateContext,
                entry,
                targetJob,
                scriptTypeName);
            candidateContext = null;
            return new BasicAcrCompileResult(loadedCompilation, diagnostics, null);
        }
        catch (Exception exception)
        {
            candidateContext?.Unload();
            return new BasicAcrCompileResult(
                null,
                diagnostics,
                exception.GetBaseException().Message);
        }
    }

    private static bool IsAllowedAssembly(string name) =>
        name is "HiAuRo" or "HiAuRo.Helper" ||
        AllowedAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    private static BasicAcrDiagnostic ToBasicDiagnostic(Diagnostic diagnostic)
    {
        if (!diagnostic.Location.IsInSource)
            return new BasicAcrDiagnostic(diagnostic.GetMessage(), null, null, null);

        var lineSpan = diagnostic.Location.GetLineSpan();
        return new BasicAcrDiagnostic(
            diagnostic.GetMessage(),
            lineSpan.Path,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1);
    }
}
