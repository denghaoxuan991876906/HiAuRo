using OmenTools.OmenService;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;

namespace HiAuRo;

public static partial class Data
{
    /// <summary>
    /// 战斗状态数据 —— 转发 GameState.* + Condition.*
    /// </summary>
    public static class Combat
    {
        /// <summary>当前 GCD 窗口已使用的能力技数量（框架内部设置，ACR 只读）</summary>
        public static int AbilityCountInGcd { get; internal set; }

        /// <summary>当前 GCD 窗口能力技上限（ACR 可读写，框架仅在生命周期事件时重置为 PluginConfig 默认值）</summary>
        public static int MaxAbilityTimesInGcd { get; set; } = 2;

        /// <summary>上一次 GCD 技能施放成功的时间戳 (Environment.TickCount64)</summary>
        public static long LastGCDCompleted { get; internal set; }

        /// <summary>上一次能力技施放成功的时间戳 (Environment.TickCount64)</summary>
        public static long LastAbilityUseTime { get; internal set; }

        /// <summary>能力技间隔是否已过 (用于 oGCD slot 构建判定)</summary>
        public static bool AbilityIntervalElapsed =>
            LastAbilityUseTime == 0
            || Environment.TickCount64 - LastAbilityUseTime >= PluginConfig.Instance.AbilityIntervalMs;

        public static bool IsLoggedIn => GameState.IsLoggedIn;

        public static bool IsInInstanceArea => GameState.IsInInstanceArea;

        public static bool IsInPVPArea => GameState.IsInPVPArea;

        public static uint TerritoryType => GameState.TerritoryType;

        public static uint Map => GameState.Map;

        public static float DeltaTime => GameState.DeltaTime;

        public static long ServerTimeUnix => GameState.ServerTimeUnix;

        /// <summary>
        /// 是否在战斗中 — 读角色对象 StatusFlags。
        /// 使用原生内存 Struct->InCombat，副本录像回放下也能正常工作。
        /// </summary>
        public static bool InCombat =>
            Me.Object is ICharacter c && c.StatusFlags.HasFlag(StatusFlags.InCombat);

        public static bool IsCasting =>
            DService.Instance().Condition is { } cond && cond[ConditionFlag.Casting];

        public static bool IsBoundByDuty =>
            DService.Instance().Condition is { } cond && cond[ConditionFlag.BoundByDuty];

        public static bool IsOnMount =>
            DService.Instance().Condition is { } cond && cond[ConditionFlag.Mounted];

        public static bool IsBetweenAreas =>
            DService.Instance().Condition is { } cond && cond[ConditionFlag.BetweenAreas];
    }
}
