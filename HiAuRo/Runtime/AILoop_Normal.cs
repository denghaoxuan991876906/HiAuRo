using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// PVE 正常循环 —— 对齐 AE
/// Check() 不区分 GCD/oGCD 窗口，所有 Resolver 每帧都调用
/// SlotMode 只控制 Build/执行时机，不控制 Check()
/// </summary>
public sealed class AILoop_Normal : IAILoop
{
    private readonly List<SlotResolverData> _resolvers;
    private readonly List<ResolverDebugInfo> _debugInfos;
    private int[] _checkResults = []; // 复用数组，避免每帧 new 分配

    /// <summary>ACR Debug 面板数据（每帧刷新，只读访问）</summary>
    public IReadOnlyList<ResolverDebugInfo> DebugResolvers => _debugInfos;

    /// <summary>Initializes a new instance of the <see cref="AILoop_Normal"/> class</summary>
    public AILoop_Normal(List<SlotResolverData> resolvers)
    {
        _resolvers = resolvers;
        _debugInfos = new List<ResolverDebugInfo>(resolvers.Count);
        foreach (var data in resolvers)
            _debugInfos.Add(new ResolverDebugInfo
            {
                Name = data.Resolver.GetType().Name,
                Mode = data.Mode
            });
    }

    /// <summary>获取下一个待执行的 Slot</summary>
    public Slot? GetNextSlot(bool blockBuild = false)
    {
        // 每帧重置 debug 数据
        foreach (var info in _debugInfos)
        {
            info.CheckResult = -99;
            info.CheckThrew = false;
            info.CheckError = "";
            info.PassedWindow = false;
            info.BuiltSlot = false;
            info.BuiltSkills = "";
        }

        if (_resolvers.Count == 0)
        {
            DService.Instance().Log.Error("[AILoop] 没有已注册的 SlotResolver");
            return null;
        }

        // 无目标时不调 Check，ACR 作者在 OnNoTarget 中自行处理
        if (Data.Target.Current == null)
        {
            foreach (var info in _debugInfos)
            {
                info.CheckResult = -99;
                info.CheckThrew = false;
                info.CheckError = "";
                info.PassedWindow = false;
                info.BuiltSlot = false;
                info.BuiltSkills = "";
            }
            return null;
        }

        bool isGcdReady = GCDHelper.CanUseGCD();
        bool isOffGcdWindow = GCDHelper.CanUseOffGcd();
        bool is2ndAbWindow = GCDHelper.Is2ndAbilityTime();
        float gcdRemain = GCDHelper.GetGCDCooldown();

        // ── 第一遍：遍历所有 Resolver，执行 Check()，记录结果 ──
        if (_checkResults.Length != _resolvers.Count)
            _checkResults = new int[_resolvers.Count];
        for (int i = 0; i < _resolvers.Count; i++)
        {
            var data = _resolvers[i];
            var info = _debugInfos[i];

            int checkResult;
            try
            {
                checkResult = data.Resolver.Check();
                info.CheckResult = checkResult;
            }
            catch (Exception ex)
            {
                checkResult = -99;
                info.CheckResult = -99;
                info.CheckThrew = true;
                info.CheckError = ex.Message;
                DService.Instance().Log.Error($"[AILoop] Check#{data.Resolver.GetType().Name} 异常: {ex}");
            }
            _checkResults[i] = checkResult;
        }

        // 暂停/停止时只阻断 Build，Check 已全部执行完毕
        if (blockBuild) return null;

        // ── 第二遍：按顺序找到第一个 Check >= 0 且窗口匹配的 Resolver，调 Build ──
        for (int i = 0; i < _resolvers.Count; i++)
        {
            if (_checkResults[i] < 0) continue;

            var data = _resolvers[i];
            var info = _debugInfos[i];

            // 窗口判定：Mode 控制 Build/执行时机
            bool canExecute = data.Mode switch
            {
                SlotMode.Gcd    => isGcdReady,
                SlotMode.OffGcd => isOffGcdWindow
                    && Data.Combat.AbilityIntervalElapsed
                    && Data.Combat.AbilityCountInGcd < Data.Combat.MaxAbilityTimesInGcd,
                SlotMode.Always => true,
                _              => false
            };

            if (!canExecute)
            {
                info.PassedWindow = false;
                continue;
            }

            info.PassedWindow = true;

            // Build + 执行
            try
            {
                var slot = new Slot();
                data.Resolver.Build(slot);
                var resolverName = data.Resolver.GetType().Name;

                info.BuiltSlot = true;
                info.BuiltSkills = string.Join(",", slot.Actions.Select(a => a.Spell.Name));

                DService.Instance().Log.Debug($"[AILoop] Build: {resolverName} → {slot.Actions.Count}个技能 = [{string.Join(", ", slot.Actions.Select(a => $"{a.Spell.Name}({a.Spell.Id})"))}] (GCD={isGcdReady} oGCD={isOffGcdWindow} GCDr={gcdRemain:F0}ms 2ndAb={is2ndAbWindow} ab={Data.Combat.AbilityCountInGcd}/{Data.Combat.MaxAbilityTimesInGcd})");

                if (data.Mode == SlotMode.Gcd)
                    Data.Combat.AbilityCountInGcd = 0;
                else if (slot.Actions.Any(a => a.Spell.IsAbility()))
                    Data.Combat.AbilityCountInGcd++;

                return slot;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[AILoop] Build error: {ex.Message}\n{ex.StackTrace}");
            }

            // Build 成功会 return，到这里的只有 Build 抛异常，继续找下一个
        }

        return null;
    }
}
