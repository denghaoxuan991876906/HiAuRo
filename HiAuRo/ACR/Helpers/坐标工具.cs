using System.Numerics;

namespace HiAuRo.ACR;

/// <summary>
/// 坐标计算工具 —— FFXIV 坐标系（0°=北=-Z，90°=东=+X）
/// </summary>
public static class 坐标工具
{
    /// <summary>角度（度）→ XZ 平面方向向量（0°=北=-Z）</summary>
    public static Vector3 方向向量(float 角度度)
    {
        var rad = 角度度 * MathF.PI / 180f;
        return new Vector3(MathF.Sin(rad), 0, -MathF.Cos(rad));
    }

    /// <summary>两点间方位角（度），0°=北，90°=东</summary>
    public static float 方位角(Vector3 从, Vector3 到)
    {
        var dx = 到.X - 从.X;
        var dz = 到.Z - 从.Z;
        var rad = MathF.Atan2(dx, -dz);
        return 角度归一化(rad * 180f / MathF.PI);
    }

    /// <summary>XZ 平面距离（忽略 Y）</summary>
    public static float 平面距离(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>XZ 平面距离平方（忽略 Y，免开根）</summary>
    public static float 平面距离平方(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    /// <summary>从原点沿指定方向移动指定距离</summary>
    public static Vector3 沿方向移动(Vector3 原点, float 角度度, float 距离)
    {
        var dir = 方向向量(角度度);
        return new Vector3(原点.X + dir.X * 距离, 原点.Y, 原点.Z + dir.Z * 距离);
    }

    /// <summary>从起点向目标方向延伸指定距离（A→B 方向上再走 C 米）</summary>
    public static Vector3 向目标延伸(Vector3 起点, Vector3 目标, float 距离)
    {
        var dx = 目标.X - 起点.X;
        var dz = 目标.Z - 起点.Z;
        var len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 0.0001f) return 起点; // 重合时返回起点
        return new Vector3(起点.X + dx / len * 距离, 起点.Y, 起点.Z + dz / len * 距离);
    }

    /// <summary>判断点是否在扇形区域内</summary>
    public static bool 在扇形内(Vector3 点, Vector3 中心, float 朝向度, float 半张角度, float 半径)
    {
        var dx = 点.X - 中心.X;
        var dz = 点.Z - 中心.Z;
        if (dx * dx + dz * dz > 半径 * 半径) return false;

        var angle = MathF.Atan2(dx, -dz);
        var facingRad = 朝向度 * MathF.PI / 180f;
        var halfRad = 半张角度 * MathF.PI / 180f;
        var diff = MathHelper.NormalizeAngle(angle - facingRad);
        return MathF.Abs(diff) <= halfRad;
    }

    /// <summary>角度归一化到 [0, 360)</summary>
    public static float 角度归一化(float 度)
    {
        var a = 度 % 360f;
        return a < 0f ? a + 360f : a;
    }

    /// <summary>绕中心点旋转（逆时针为正，XZ 平面）</summary>
    public static Vector3 绕点旋转(Vector3 点, Vector3 中心, float 角度度)
    {
        var rad = 角度度 * MathF.PI / 180f;
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        var dx = 点.X - 中心.X;
        var dz = 点.Z - 中心.Z;
        return new Vector3(中心.X + dx * cos - dz * sin, 点.Y, 中心.Z + dx * sin + dz * cos);
    }
}
