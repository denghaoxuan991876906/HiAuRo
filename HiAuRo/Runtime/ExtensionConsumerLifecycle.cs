namespace HiAuRo.Runtime;

internal static class ExtensionConsumerLifecycle
{
    internal sealed record ReloadState(
        uint TerritoryId,
        bool ExecutionWasInitialized,
        bool ExecutionWasRunning,
        bool AssistWasInitialized,
        bool AssistWasRunning);

    internal static ReloadState Suspend(bool clearPluginWindows)
    {
        var territoryId = OmenTools.OmenService.GameState.TerritoryType;
        var executionWasInitialized = Execution.ExecutionAxis.Instance.Initialized;
        var executionWasRunning = Execution.ExecutionAxis.Instance.IsRunning;
        var assistWasInitialized = Execution.AssistAxis.Instance.Initialized;
        var assistWasRunning = Execution.AssistAxis.Instance.IsRunning;
        Execution.ExecutionAxis.Instance.Shutdown();
        Execution.AssistAxis.Instance.Shutdown();
        ACRLifecycle.Runner.CancelSlotExecution();
        try
        {
            Task.WhenAll(
                    Execution.ExecutionAxis.Instance.Quiescence,
                    Execution.AssistAxis.Instance.Quiescence,
                    ACRLifecycle.Runner.SlotQuiescence)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException) { }
        Script.ScriptManager.UnloadCurrent();
        Execution.ScriptCompiler.ClearCache();
        Execution.ExecutionJsonLoader.Clear();
        if (clearPluginWindows)
            PluginWindowManager.Clear();
        return new ReloadState(
            territoryId,
            executionWasInitialized,
            executionWasRunning,
            assistWasInitialized,
            assistWasRunning);
    }

    internal static void Resume(
        ReloadState state,
        string configDir,
        bool refreshPluginWindows)
    {
        Execution.ExecutionJsonLoader.RegisterBuiltInTypes();
        Execution.TriggerCatalogExporter.RebuildRuntimeTypeRegistry();
        Execution.ScriptCompiler.RefreshAssemblyBindings();
        if (state.ExecutionWasInitialized)
            Execution.ExecutionAxis.Instance.Init();
        if (state.AssistWasInitialized)
            Execution.AssistAxis.Instance.Init();
        if (state.ExecutionWasRunning)
            Execution.ExecutionAxis.Instance.Start();
        if (state.AssistWasRunning)
            Execution.AssistAxis.Instance.Start();
        ACRLifecycle.Runner.ResumeSlotGeneration();

        if (state.TerritoryId != 0)
            Script.ScriptManager.LoadTerritory(state.TerritoryId);
        if (refreshPluginWindows)
            PluginWindowManager.Refresh();

        Execution.TriggerCatalogExporter.ExportToConfigDirectory(configDir);
    }
}
