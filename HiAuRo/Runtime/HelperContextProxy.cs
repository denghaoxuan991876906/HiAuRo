using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HiAuRo.ACR;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;

namespace HiAuRo.Runtime;

/// <summary>
/// HelperRuntime 上下文实现 —— 通过 DService 提供游戏数据查询，
/// 在 Helper 的 ALC 内用 Reflection.Emit 生成名为 "HiAuRo" 的程序集实现 IHelperContext，
/// 利用 InternalsVisibleTo("HiAuRo") 授权访问 internal 接口
/// </summary>
sealed class HiAuRoContextImpl
{
    public bool HasStatus(uint statusId)
    {
        return AuraHelper.HasAura(Data.Me.Object,statusId);
    }

    public bool HasStatusOnTarget(uint statusId)
    {
        var target = Data.Target.Current;
        if (target == null) return false;
        
        return AuraHelper.HasAura(target, statusId);
    }

    public T? GetGauge<T>() where T : class
    {
        return (T?)GetGauge(typeof(T));
    }

    public object? GetGauge(Type t)
    {
        var gauges = DService.Instance().JobGauges;
        var method = gauges.GetType().GetMethod(nameof(gauges.Get))?.MakeGenericMethod(t);
        return method?.Invoke(gauges, null);
    }

    // ── Buff 查询 ──

    public float GetAuraTimeLeft(uint buffId)
    {
        return ACR.AuraHelper.GetAuraTimeLeft(Data.Me.Object, buffId);
    }

    public int GetAuraStackCount(uint buffId)
    {
        if (Data.Me.Object is not IBattleChara bc) return 0;
        foreach (var s in bc.StatusList)
        {
            if (s.StatusID == buffId)
                return s.Param > 0 ? s.Param : 1;
        }
        return 0;
    }

    // ── CD 查询 ──

    public float GetCharges(uint spellId) => ACR.SpellHelper.GetCharges(spellId);

    public float GetCooldownRemaining(uint spellId) => ACR.SpellHelper.GetCooldownRemaining(spellId);

    // ── Combo / GCD ──

    public uint GetLastComboSpellId() => ACR.ComboHelper.LastComboSpellId;

    public int GetGCDCooldown() => (int)ACR.GCDHelper.GetGCDCooldown();

    // ── 技能历史 ──

    public bool RecentlyUsedSpell(uint spellId, int ms) =>
        ACR.SpellHistoryHelper.RecentlyUsed(spellId, ms);

    // ── 战斗状态 ──

    public bool IsMoving() => Data.Me.IsMoving;

    public bool IsInCombat() => Data.Combat.InCombat;

    public int GetNearbyEnemyCount(float range)
    {
        var self = Data.Me.Object;
        if (self == null) return 0;
        int count = 0;
        var enemies = Data.Objects.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (Data.Me.DistanceToObject2D(enemies[i]) <= range)
                count++;
        }
        return count;
    }

    public int GetEnemyCountNearTarget(float range)
    {
        var target = Data.Target.Current;
        return ACR.TargetHelper.GetNearbyEnemyCount(target, range);
    }

    // ── 自身属性 ──

    public float GetHPPercent()
    {
        if (Data.Me.Object is not IBattleChara bc || bc.MaxHp == 0) return 100f;
        return (float)bc.CurrentHp / bc.MaxHp * 100f;
    }

    public int GetCurrentLevel() => Data.Me.CurrentLevel;

    // ── 目标 ──

    public bool IsCurrentTargetInvincible()
    {
        var target = Data.Target.Current;
        return target == null || target.IsDead == true || !target.IsTargetable || AuraHelper.HasInvincibleBuffs(target);
    }

    // ── 技能数据 ──

    public uint GetActionChange(uint spellId) =>
        ACR.SpellExtension.GetActionChange(spellId);

    // ── 队伍查询 ──

    public int GetPartyCount() => Data.Party.All.Count;

    private static HiAuRo.Data.PartyMemberInfo? GetPartyMember(int index)
    {
        if (index < 0 || index >= Data.Party.All.Count) return null;
        return Data.Party.All[index];
    }

    public bool IsPartyMemberAlive(int index) =>
        GetPartyMember(index)?.IsAlive ?? false;

    public float GetPartyMemberHP(int index)
    {
        var member = GetPartyMember(index);
        if (member?.Player is not IBattleChara bc) return 0f;
        return bc.CurrentHp;
    }

    public float GetPartyMemberMaxHP(int index)
    {
        var member = GetPartyMember(index);
        if (member?.Player is not IBattleChara bc) return 0f;
        return bc.MaxHp;
    }

    public float GetPartyMemberHPPercent(int index)
    {
        var member = GetPartyMember(index);
        if (member?.Player is not IBattleChara bc || bc.MaxHp == 0) return 0f;
        return (float)bc.CurrentHp / bc.MaxHp;
    }
}

/// <summary>
/// Reflection.Emit 适配器 —— 在 Helper ALC 内生成程序集（名称 "HiAuRo"），
/// 匹配 Helper 的 InternalsVisibleTo，实现 IHelperContext 并委托到 HiAuRoContextImpl
/// </summary>
static class EmitProxy
{
    /// <summary>创建 Helper ALC 内的 IHelperContext 实现实例</summary>
    public static object Create(HiAuRoContextImpl impl, Assembly helperAsm, AssemblyLoadContext helperAlc)
    {
        var ctxType = helperAsm.GetType("HiAuRo.Helper.IHelperContext")!;

        using (helperAlc.EnterContextualReflection())
        {
            var asmName = new AssemblyName("HiAuRo");
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var modBuilder = asmBuilder.DefineDynamicModule("Main");
            var typeBuilder = modBuilder.DefineType("HelperContextAdapter", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed);
            typeBuilder.AddInterfaceImplementation(ctxType);

            // 静态委托字段定义
            var fldDefs = new (string Name, Type DelegateType)[]
            {
                ("OnHasStatus",              typeof(Func<uint, bool>)),
                ("OnHasStatusOnTarget",      typeof(Func<uint, bool>)),
                ("OnGetGauge",               typeof(Func<Type, object?>)),
                ("OnGetAuraTimeLeft",        typeof(Func<uint, float>)),
                ("OnGetAuraStackCount",      typeof(Func<uint, int>)),
                ("OnGetCharges",             typeof(Func<uint, float>)),
                ("OnGetCooldownRemaining",   typeof(Func<uint, float>)),
                ("OnGetLastComboSpellId",    typeof(Func<uint>)),
                ("OnGetGCDCooldown",         typeof(Func<int>)),
                ("OnRecentlyUsedSpell",      typeof(Func<uint, int, bool>)),
                ("OnIsMoving",               typeof(Func<bool>)),
                ("OnIsInCombat",             typeof(Func<bool>)),
                ("OnGetNearbyEnemyCount",    typeof(Func<float, int>)),
                ("OnGetEnemyCountNearTarget",typeof(Func<float, int>)),
                ("OnGetHPPercent",           typeof(Func<float>)),
                ("OnGetCurrentLevel",        typeof(Func<int>)),
                ("OnIsCurrentTargetInvincible", typeof(Func<bool>)),
                ("OnGetActionChange",        typeof(Func<uint, uint>)),
                ("OnGetPartyCount",          typeof(Func<int>)),
                ("OnIsPartyMemberAlive",     typeof(Func<int, bool>)),
                ("OnGetPartyMemberHP",       typeof(Func<int, float>)),
                ("OnGetPartyMemberMaxHP",    typeof(Func<int, float>)),
                ("OnGetPartyMemberHPPercent", typeof(Func<int, float>)),
            };

            var fields = new Dictionary<string, FieldBuilder>();
            foreach (var (name, delegateType) in fldDefs)
            {
                fields[name] = typeBuilder.DefineField(name, delegateType,
                    FieldAttributes.Public | FieldAttributes.Static);
            }

            var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle")!;

            // 普通方法（非泛型）— 使用 EmitSimpleCall
            var simpleMethods = new (string Name, string FieldName, int ParamCount)[]
            {
                ("HasStatus",              "OnHasStatus",              1),
                ("HasStatusOnTarget",      "OnHasStatusOnTarget",      1),
                ("GetAuraTimeLeft",        "OnGetAuraTimeLeft",        1),
                ("GetAuraStackCount",      "OnGetAuraStackCount",      1),
                ("GetCharges",             "OnGetCharges",             1),
                ("GetCooldownRemaining",   "OnGetCooldownRemaining",   1),
                ("GetLastComboSpellId",    "OnGetLastComboSpellId",    0),
                ("GetGCDCooldown",         "OnGetGCDCooldown",         0),
                ("RecentlyUsedSpell",      "OnRecentlyUsedSpell",      2),
                ("IsMoving",               "OnIsMoving",               0),
                ("IsInCombat",             "OnIsInCombat",             0),
                ("GetNearbyEnemyCount",    "OnGetNearbyEnemyCount",    1),
                ("GetEnemyCountNearTarget","OnGetEnemyCountNearTarget",1),
                ("GetHPPercent",           "OnGetHPPercent",           0),
                ("GetCurrentLevel",        "OnGetCurrentLevel",        0),
                ("IsCurrentTargetInvincible", "OnIsCurrentTargetInvincible", 0),
                ("GetActionChange",        "OnGetActionChange",        1),
                ("GetPartyCount",          "OnGetPartyCount",          0),
                ("IsPartyMemberAlive",     "OnIsPartyMemberAlive",     1),
                ("GetPartyMemberHP",       "OnGetPartyMemberHP",       1),
                ("GetPartyMemberMaxHP",    "OnGetPartyMemberMaxHP",    1),
                ("GetPartyMemberHPPercent","OnGetPartyMemberHPPercent",1),
            };

            foreach (var (methodName, fieldName, paramCount) in simpleMethods)
            {
                var ifaceMethod = ctxType.GetMethod(methodName)!;
                EmitDelegateCall(typeBuilder, ctxType, ifaceMethod, fields[fieldName], paramCount);
            }

            // --- GetGauge<T> (泛型方法) ---
            {
                var ifaceMethod = ctxType.GetMethod("GetGauge")!;
                var fld = fields["OnGetGauge"];
                var invokeMethod = fld.FieldType.GetMethod("Invoke")!;
                var method = typeBuilder.DefineMethod("GetGauge",
                    MethodAttributes.Public | MethodAttributes.Virtual);
                var gParams = method.DefineGenericParameters("T");
                gParams[0].SetGenericParameterAttributes(GenericParameterAttributes.ReferenceTypeConstraint);
                method.SetReturnType(gParams[0]);

                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, fld);
                il.Emit(OpCodes.Ldtoken, gParams[0]);
                il.Emit(OpCodes.Call, getTypeFromHandle);
                il.Emit(OpCodes.Callvirt, invokeMethod);
                il.Emit(OpCodes.Unbox_Any, gParams[0]);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(method, ifaceMethod);
            }

            var proxyType = typeBuilder.CreateType()!;

            // 注入委托
            proxyType.GetField("OnHasStatus")!.SetValue(null, (Func<uint, bool>)impl.HasStatus);
            proxyType.GetField("OnHasStatusOnTarget")!.SetValue(null, (Func<uint, bool>)impl.HasStatusOnTarget);
            proxyType.GetField("OnGetGauge")!.SetValue(null, (Func<Type, object?>)impl.GetGauge);
            proxyType.GetField("OnGetAuraTimeLeft")!.SetValue(null, (Func<uint, float>)impl.GetAuraTimeLeft);
            proxyType.GetField("OnGetAuraStackCount")!.SetValue(null, (Func<uint, int>)impl.GetAuraStackCount);
            proxyType.GetField("OnGetCharges")!.SetValue(null, (Func<uint, float>)impl.GetCharges);
            proxyType.GetField("OnGetCooldownRemaining")!.SetValue(null, (Func<uint, float>)impl.GetCooldownRemaining);
            proxyType.GetField("OnGetLastComboSpellId")!.SetValue(null, (Func<uint>)impl.GetLastComboSpellId);
            proxyType.GetField("OnGetGCDCooldown")!.SetValue(null, (Func<int>)impl.GetGCDCooldown);
            proxyType.GetField("OnRecentlyUsedSpell")!.SetValue(null, (Func<uint, int, bool>)impl.RecentlyUsedSpell);
            proxyType.GetField("OnIsMoving")!.SetValue(null, (Func<bool>)impl.IsMoving);
            proxyType.GetField("OnIsInCombat")!.SetValue(null, (Func<bool>)impl.IsInCombat);
            proxyType.GetField("OnGetNearbyEnemyCount")!.SetValue(null, (Func<float, int>)impl.GetNearbyEnemyCount);
            proxyType.GetField("OnGetEnemyCountNearTarget")!.SetValue(null, (Func<float, int>)impl.GetEnemyCountNearTarget);
            proxyType.GetField("OnGetHPPercent")!.SetValue(null, (Func<float>)impl.GetHPPercent);
            proxyType.GetField("OnGetCurrentLevel")!.SetValue(null, (Func<int>)impl.GetCurrentLevel);
            proxyType.GetField("OnIsCurrentTargetInvincible")!.SetValue(null, (Func<bool>)impl.IsCurrentTargetInvincible);
            proxyType.GetField("OnGetActionChange")!.SetValue(null, (Func<uint, uint>)impl.GetActionChange);
            proxyType.GetField("OnGetPartyCount")!.SetValue(null, (Func<int>)impl.GetPartyCount);
            proxyType.GetField("OnIsPartyMemberAlive")!.SetValue(null, (Func<int, bool>)impl.IsPartyMemberAlive);
            proxyType.GetField("OnGetPartyMemberHP")!.SetValue(null, (Func<int, float>)impl.GetPartyMemberHP);
            proxyType.GetField("OnGetPartyMemberMaxHP")!.SetValue(null, (Func<int, float>)impl.GetPartyMemberMaxHP);
            proxyType.GetField("OnGetPartyMemberHPPercent")!.SetValue(null, (Func<int, float>)impl.GetPartyMemberHPPercent);

            return Activator.CreateInstance(proxyType)!;
        }
    }

    /// <summary>为接口方法生成 IL：加载委托字段 → 加载参数 → 调用委托</summary>
    private static void EmitDelegateCall(TypeBuilder typeBuilder, Type ctxType,
        MethodInfo ifaceMethod, FieldBuilder fld, int paramCount)
    {
        var paramTypes = ifaceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
        var method = typeBuilder.DefineMethod(ifaceMethod.Name,
            MethodAttributes.Public | MethodAttributes.Virtual,
            ifaceMethod.ReturnType, paramTypes);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldsfld, fld);

        // ldarg_1, ldarg_2, ...
        for (int i = 0; i < paramCount; i++)
        {
            il.Emit(i switch
            {
                0 => OpCodes.Ldarg_1,
                1 => OpCodes.Ldarg_2,
                2 => OpCodes.Ldarg_3,
                _ => throw new NotSupportedException("EmitProxy 仅支持最多 3 个参数")
            });
        }

        il.Emit(OpCodes.Callvirt, fld.FieldType.GetMethod("Invoke")!);
        il.Emit(OpCodes.Ret);
        typeBuilder.DefineMethodOverride(method, ifaceMethod);
    }
}
