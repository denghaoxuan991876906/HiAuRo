using System.Numerics;
using HiAuRo.ACR;
using HiAuRo.Infrastructure;

namespace HiAuRo.Execution.Events;

public sealed class ReceviceAbilityEffectCondParams : ITriggerCondParams
{
    public uint SourceID;
    public uint ActionId;
    public uint TargetID;
    public ushort AnimationID;
    public byte EffectType;
    public string Name = string.Empty;

    public override string ToString() =>
        $"Action={LogHelper.Action(ActionId)} Source={LogHelper.Entity(SourceID)} Target={LogHelper.Entity(TargetID)} Type={EffectType}";
}

public sealed class TetherCreateParams : ITriggerCondParams
{
    public uint TetherID;
    public uint SourceID;
    public uint TargetID;
    public byte Param2;
    public byte Param3;
    public byte Param5;

    public override string ToString() =>
        $"ID={TetherID} Source={LogHelper.Entity(SourceID)} Target={LogHelper.Entity(TargetID)} Param2={Param2} Param3={Param3} Param5={Param5}";
}

public sealed class TetherSnapshotParams : ITriggerCondParams
{
    public uint TetherID;
    public uint SourceID;
    public uint TargetID;
    public byte SlotIndex;
    public byte Progress;
    public bool IsActive;

    public override string ToString() =>
        $"ID={TetherID} Source={LogHelper.Entity(SourceID)} Target={LogHelper.Entity(TargetID)} Slot={SlotIndex} Progress={Progress} Active={IsActive}";
}

public sealed class TetherRemoveParams : ITriggerCondParams
{
    public uint SourceID;
    public byte Param2;
    public byte Param3;
    public byte Param5;

    public override string ToString() =>
        $"Source={LogHelper.Entity(SourceID)} Param2={Param2} Param3={Param3} Param5={Param5}";
}

public sealed class OnMapEffectCreateEvent : ITriggerCondParams
{
    public uint Pos;
    public ushort arg0;
    public ushort arg1;

    public override string ToString() => $"MapEffect Pos={Pos} arg0={arg0} arg1={arg1}";
}

public sealed class DirectorUpdateParams : ITriggerCondParams
{
    public Structures.DirectorUpdateCategory Category;
    public uint Param1;
    public uint Param2;
    public uint Param3;
    public uint Param4;
    public uint A6;
    public uint A7;
    public uint A8;
    public uint A9;

    public override string ToString() =>
        $"Category={Category} P1={Param1} P2={Param2} P3={Param3} P4={Param4} A6={A6} A7={A7} A8={A8} A9={A9}";
}

public sealed class ActorControlParams : ITriggerCondParams
{
    public uint SourceID;
    public ushort Command;
    public uint P1;
    public uint P2;
    public uint P3;
    public uint P4;
    public uint P5;
    public uint P6;
    public uint TargetID;

    public override string ToString() =>
        $"Cmd={Command} Source={LogHelper.Entity(SourceID)} Target={LogHelper.Entity(TargetID)} P1={P1} P2={P2} P3={P3} P4={P4} P5={P5} P6={P6}";
}

public sealed class ActorDeathParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
    public uint DataId;
    public string Name = string.Empty;

    public override string ToString() =>
        $"Source={LogHelper.Entity(SourceID)} DataId={DataId} Name=\"{Name}\"";
}

public sealed class TargetIconEffectTestCondParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
    public uint IconId;

    public override string ToString() =>
        $"IconId={IconId} Source={LogHelper.Entity(SourceID)} Target={LogHelper.Entity(TargetID)}";
}

public sealed class TargetAbleCondParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
    public bool IsTargetable;

    public override string ToString() =>
        $"Target={LogHelper.Entity(TargetID)} IsTargetable={IsTargetable}";
}

public sealed class ActorControlCombatParams : ITriggerCondParams
{
    public bool IsEntering;

    public override string ToString() => $"CombatState IsEntering={IsEntering}";
}

public sealed class PlayActionTimelineParams : ITriggerCondParams
{
    public uint SourceID;
    public uint Id;

    public override string ToString() =>
        $"Source={LogHelper.Entity(SourceID)} Id={Id}";
}

public sealed class EnemyCastSpellCondParams : ITriggerCondParams
{
    public uint SourceID;
    public uint DataId;
    public Vector3 CastPos;
    public float TotalCastTimeInSec;
    public float CastRot;
    public uint SpellId;
    public string SpellName = string.Empty;
    public long StartCastTime;

    public override string ToString() =>
        $"Spell={LogHelper.Action(SpellId)} Source={LogHelper.Entity(SourceID)} DataId={DataId} time={TotalCastTimeInSec:F1} pos={CastPos} rot={CastRot:F1}";
}

public sealed class SelfCastStartCondParams : ITriggerCondParams
{
    public uint SourceID;
    public uint SpellId;
    public float TotalCastTimeInSec;
    public long StartCastTime;

    public override string ToString() =>
        $"Spell={LogHelper.Action(SpellId)} Source={LogHelper.Entity(SourceID)} time={TotalCastTimeInSec:F1}";
}

public sealed class NpcYellCondParams : ITriggerCondParams
{
    public ulong sourceId;
    public string sourceName = string.Empty;
    public short yellId;
    public string yellMsg = string.Empty;

    public override string ToString() =>
        $"Source={sourceName}({sourceId}) yellId={yellId} msg=\"{yellMsg}\"";
}

public sealed class OnEnvControlEvent : ITriggerCondParams
{
    public uint Index;
    public uint Flag;

    public override string ToString() => $"EnvControl Index={Index} Flag={Flag}";
}

public sealed class ReceviceNoTargetAbilityEffectCondParams : ITriggerCondParams
{
    public uint SourceID;
    public uint ActionId;
    public string Name = string.Empty;
    public Vector3 pos;

    public override string ToString() =>
        $"Action={LogHelper.Action(ActionId)} Source={LogHelper.Entity(SourceID)} pos={pos}";
}

public sealed class UnitCreateCondParams : ITriggerCondParams
{
    public uint EntityId;
    public uint DataId;
    public string Name = string.Empty;
    public Vector3 Position;

    public override string ToString() =>
        $"Name=\"{Name}\" DataId={DataId} Entity={LogHelper.Entity(EntityId)} Pos={Position}";
}

public sealed class UnitDeleteCondParams : ITriggerCondParams
{
    public uint EntityId;
    public uint DataId;
    public string Name = string.Empty;

    public override string ToString() =>
        $"Name=\"{Name}\" DataId={DataId} Entity={LogHelper.Entity(EntityId)}";
}

public sealed class WeatherChangedCondParams : ITriggerCondParams
{
    public int NewWeatherId;

    public override string ToString() => $"天气变化 NewWeatherId={NewWeatherId}";
}

public sealed class AddStatusCondParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
    public uint StatusId;
    public string StatusName = string.Empty;
    public uint count;
    public ushort Param;
    public float RemainingTime;

    public override string ToString() =>
        $"Status={LogHelper.Status(StatusId)} Source={LogHelper.Entity(SourceID)} Target={LogHelper.Entity(TargetID)} count={count} Param={Param} time={RemainingTime:F1}";
}

public sealed class RemoveStatusCondParams : ITriggerCondParams
{
    public uint TargetID;
    public uint StatusId;
    public string StatusName = string.Empty;

    public override string ToString() =>
        $"Status={LogHelper.Status(StatusId)} Target={LogHelper.Entity(TargetID)}";
}

public sealed class AfterSpellCondParams : ITriggerCondParams
{
    public uint SpellId;

    public override string ToString() =>
        $"Spell={LogHelper.Action(SpellId)}";
}

public sealed class CombatStateParams : ITriggerCondParams
{
    public bool IsEntering;

    public override string ToString() => $"CombatState IsEntering={IsEntering}";
}

public sealed class ObjectChangeParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
    public ushort Command;
    public uint P1;
    public uint P2;
    public uint P3;
    public uint P4;

    public override string ToString() =>
        $"Cmd={Command} Source={LogHelper.Entity(SourceID)} Target={LogHelper.Entity(TargetID)} P1={P1} P2={P2} P3={P3} P4={P4}";
}

public sealed class ObjectEffectParams : ITriggerCondParams
{
    public uint ObjectID;
    public ushort Data1;
    public ushort Data2;

    public override string ToString() =>
        $"Obj={LogHelper.Entity(ObjectID)} Data1={Data1} Data2={Data2}";
}

public sealed class ObjectSpawnParams : ITriggerCondParams
{
    public uint EntityId;
    public uint DataId;
    public uint LayoutId;
    public Vector3 Position;

    public override string ToString() =>
        $"ObjectSpawn DataId={DataId} Entity={LogHelper.Entity(EntityId)} Layout={LayoutId} Pos={Position}";
}

public sealed class BattleStartCondParams : ITriggerCondParams
{
    public override string ToString() => "战斗开始";
}

public sealed class BattleEndCondParams : ITriggerCondParams
{
    public override string ToString() => "战斗结束";
}

public sealed class GameLogCondParams : ITriggerCondParams
{
    public string Log = string.Empty;
    public int LogType;

    public override string ToString() => $"Type={LogType} Log=\"{Log}\"";
}
