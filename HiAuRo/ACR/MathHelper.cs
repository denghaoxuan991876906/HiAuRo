using System.Numerics;

namespace HiAuRo.ACR;

/// <summary>
/// 向量/角度数学工具 —— ACR 位置/朝向/扇形判定
/// </summary>
public static class MathHelper
{
    public const float Rad2Deg = 180f / MathF.PI;
    public const float Deg2Rad = MathF.PI / 180f;

    /// <summary>弧度 → 角度</summary>
    public static float Radians(float degrees) => degrees * Deg2Rad;

    /// <summary>角度 → 弧度</summary>
    public static float Degrees(float radians) => radians * Rad2Deg;

    /// <summary>绕 Y 轴旋转向量</summary>
    public static Vector3 Rotate(Vector3 v, float angleDeg)
    {
        var rad = angleDeg * Deg2Rad;
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        return new Vector3(v.X * cos - v.Z * sin, v.Y, v.X * sin + v.Z * cos);
    }

    /// <summary>根据朝向角获取方向向量 (XZ 平面)</summary>
    public static Vector2 GetDir(float rotation)
        => new(MathF.Cos(rotation), MathF.Sin(rotation));

    /// <summary>根据朝向角获取 3D 方向向量</summary>
    public static Vector3 GetDirV3(float rotation)
        => new(MathF.Cos(rotation), 0, MathF.Sin(rotation));

    /// <summary>两点间相对角度（弧度）</summary>
    public static float GetRelativeAngle(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        return MathF.Atan2(dz, dx);
    }

    /// <summary>Vector2 → Vector3 (XZ)</summary>
    public static Vector3 ToVector3(this Vector2 v) => new(v.X, 0, v.Y);

    /// <summary>Vector3 → Vector2 (XZ)</summary>
    public static Vector2 ToVector2(this Vector3 v) => new(v.X, v.Z);

    /// <summary>计算以 center 为中心、朝向 dir 的扇形内包含敌人数量</summary>
    public static int CountInSector(Vector3 center, float dirRad, float radius, float halfAngleDeg)
    {
        var half = halfAngleDeg * Deg2Rad;
        int count = 0;
        foreach (var obj in Data.Objects.Enemies)
        {
            var delta = obj.Position - center;
            delta.Y = 0;
            if (delta.Length() > radius) continue;

            var angle = MathF.Atan2(delta.Z, delta.X);
            var diff = NormalizeAngle(angle - dirRad);
            if (MathF.Abs(diff) <= half) count++;
        }
        return count;
    }

    /// <summary>规范化角度到 [-PI, PI]</summary>
    public static float NormalizeAngle(float rad)
    {
        while (rad > MathF.PI) rad -= 2 * MathF.PI;
        while (rad < -MathF.PI) rad += 2 * MathF.PI;
        return rad;
    }

    /// <summary>极坐标转笛卡尔</summary>
    public static Vector3 PolarToCartesian(float radius, float angleDeg, float centerX, float centerY, float centerZ)
    {
        var rad = angleDeg * Deg2Rad;
        return new Vector3(centerX + radius * MathF.Cos(rad), centerY, centerZ + radius * MathF.Sin(rad));
    }
}
