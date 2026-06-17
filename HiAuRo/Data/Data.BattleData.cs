using System.Numerics;

namespace HiAuRo;

public static partial class Data
{
    /// <summary>
    /// 战斗事件历史缓冲区 —— 为 TriggerCond 提供近期事件查询
    /// 通过 OmenTools GamePacketManager 订阅游戏事件包填充数据
    /// </summary>
    public static class BattleData
    {
        public record TetherEvent(uint TetherId, uint SourceId, uint TargetId, long TimestampMs);
        public record ActionEffectEvent(uint ActionId, uint CasterId, ulong TargetId, long TimestampMs);
        public record MapEffectEvent(uint EffectId, Vector3 Position, long TimestampMs);

        private static readonly List<TetherEvent> _recentTethers = [];
        private static readonly List<ActionEffectEvent> _recentActionEffects = [];
        private static readonly List<MapEffectEvent> _recentMapEffects = [];
        private static readonly object _lock = new();

        private const long MaxAgeMs = 30_000;

        /// <summary>获取近期连线事件快照</summary>
        public static List<TetherEvent> GetRecentTethers() { lock (_lock) return [.. _recentTethers]; }

        /// <summary>获取近期技能效果事件快照</summary>
        public static List<ActionEffectEvent> GetRecentActionEffects() { lock (_lock) return [.. _recentActionEffects]; }

        /// <summary>获取近期地图特效事件快照</summary>
        public static List<MapEffectEvent> GetRecentMapEffects() { lock (_lock) return [.. _recentMapEffects]; }

        /// <summary>清除超过 MaxAgeMs 的过期事件</summary>
        private static void PruneExpired(long nowMs)
        {
            var cutoff = nowMs - MaxAgeMs;
            _recentTethers.RemoveAll(e => e.TimestampMs < cutoff);
            _recentActionEffects.RemoveAll(e => e.TimestampMs < cutoff);
            _recentMapEffects.RemoveAll(e => e.TimestampMs < cutoff);
        }

        /// <summary>注册连线事件</summary>
        public static void OnTetherAdded(uint tetherId, uint sourceId, uint targetId, long nowMs)
        {
            lock (_lock)
            {
                PruneExpired(nowMs);
                _recentTethers.Add(new TetherEvent(tetherId, sourceId, targetId, nowMs));
            }
        }

        /// <summary>注册行动效果事件</summary>
        public static void OnActionEffect(uint actionId, uint casterId, ulong targetId, long nowMs)
        {
            lock (_lock)
            {
                PruneExpired(nowMs);
                _recentActionEffects.Add(new ActionEffectEvent(actionId, casterId, targetId, nowMs));
            }
        }

        /// <summary>注册地图特效事件</summary>
        public static void OnMapEffect(uint effectId, Vector3 position, long nowMs)
        {
            lock (_lock)
            {
                PruneExpired(nowMs);
                _recentMapEffects.Add(new MapEffectEvent(effectId, position, nowMs));
            }
        }

        /// <summary>移除匹配的连线事件（连线消失时调用）</summary>
        public static void RemoveTethers(Predicate<TetherEvent> match)
        {
            lock (_lock) _recentTethers.RemoveAll(match);
        }
    }
}
