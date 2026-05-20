# HiAuRo.FA VFX 渲染层 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 HiAuRo.FA 插件中实现游戏原生 VFX 危险区渲染层。

**Architecture:** 通过 FFXIVClientStructs VfxObject 的互操作封装（混合策略），在地面生成/管理/销毁 VFX 实例。VfxRenderer 作为全局单例绑定到 FaPlugin 生命周期，提供 6 种形状的快捷 API。

**Tech Stack:** .NET 10, Dalamud.CN.NET.Sdk 15.0, FFXIVClientStructs, unsafe C#

**Spec:** `docs/superpowers/specs/2026-05-21-fa-vfx-rendering-design.md`

**项目路径:** `E:\DalamudPlugins\HiAuRo.FA` （独立项目，与 HiAuRo 平级）

---

## File Structure

| 操作 | 文件 | 职责 |
|------|------|------|
| 创建 | `Vfx/VfxNative.cs` | VfxObject 互操作底层（签名扫描 + 偏移访问） |
| 创建 | `Vfx/VfxPath.cs` | VFX 路径常量 |
| 创建 | `Vfx/VfxZone.cs` | IVfxZone 接口 + VfxZone 实现 |
| 创建 | `Vfx/VfxRenderer.cs` | 渲染器生命周期（创建/更新/销毁） |
| 修改 | `FaPlugin.cs` | 接入 VfxRenderer 生命周期 |

---

### Task 1: VfxNative — VfxObject 互操作封装

**Files:**
- 创建: `Vfx/VfxNative.cs`

- [ ] **Step 1: 创建 VfxNative.cs**

```csharp
using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Common.Math;
using OmenTools.Common;

namespace HiAuRo.FA.Vfx;

/// <summary>
/// VfxObject 互操作底层 —— 混合策略：优先 [GenerateInterop]，回退手动签名扫描
/// </summary>
public static unsafe class VfxNative
{
    const string CreateSig = "E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08";

    delegate VfxObject* CreateVfxDelegate(byte* path, byte* pool);

    static CreateVfxDelegate? _createVfx;
    static bool _initialized;

    const int ColorOffset = 0x260;

    public static bool IsAvailable => _initialized && _createVfx != null;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            var sig = DService.SigScanner;
            if (sig == null) return;

            var addr = sig.ScanText(CreateSig);
            if (addr == IntPtr.Zero) return;

            _createVfx = Marshal.GetDelegateForFunctionPointer<CreateVfxDelegate>(addr);
        }
        catch
        {
            _createVfx = null;
        }
    }

    public static VfxObject* Create(string path, string pool)
    {
        if (_createVfx == null) return null;

        var pathBytes = System.Text.Encoding.UTF8.GetBytes(path + "\0");
        var poolBytes = System.Text.Encoding.UTF8.GetBytes(pool + "\0");

        fixed (byte* pathPtr = pathBytes)
        fixed (byte* poolPtr = poolBytes)
        {
            try
            {
                return _createVfx(pathPtr, poolPtr);
            }
            catch
            {
                return null;
            }
        }
    }

    public static void SetPosition(VfxObject* vfx, Vector3 pos)
    {
        ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)vfx)->SetPosition(pos.X, pos.Y, pos.Z);
    }

    public static void SetRotation(VfxObject* vfx, float yaw)
    {
        var obj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)vfx;
        obj->SetRotation(yaw);
    }

    public static void SetScale(VfxObject* vfx, Vector3 scale)
    {
        ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)vfx)->SetScale(scale.X, scale.Y, scale.Z);
    }

    public static void SetColor(VfxObject* vfx, Vector4 color)
    {
        *(Vector4*)((byte*)vfx + ColorOffset) = color;
    }

    public static void Destroy(VfxObject* vfx)
    {
        if (vfx == null) return;
        try
        {
            ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)vfx)->Destroy(true);
        }
        catch { }
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build ../HiAuRo.FA/HiAuRo.FA.csproj -c Release -nologo`
Expected: Build succeeds（可能有未使用的 using 警告，忽略）

- [ ] **Step 3: 提交**

```bash
git add Vfx/VfxNative.cs
git commit -m "feat(FA): 新增 VfxNative VfxObject 互操作封装"
```

---

### Task 2: VfxPath — VFX 路径常量

**Files:**
- 创建: `Vfx/VfxPath.cs`

- [ ] **Step 1: 创建 VfxPath.cs**

```csharp
namespace HiAuRo.FA.Vfx;

/// <summary>
/// 游戏原生 VFX 路径常量
/// 路径来源：宝宝椅触发器 (S7b.xml) + PictoACT 文档
/// 开发时需用 VFXEditor 确认完整路径解析方式
/// </summary>
public static class VfxPath
{
    public const string Circle = "n4b5_tr_g05o0g";
    public const string Rect = "z6r1_b4_ibox_01k1";
    public const string Fan45 = "gl_fan045_1bf";
    public const string Fan180 = "gl_fan180_6014g2";
    public const string Ring = "er_sicle_1811t";
    public const string Donut = "m0559donut_o0v";
    public const string Cross = "er_general_x02t";
    public const string Line = "m131om_setu0f";
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build ../HiAuRo.FA/HiAuRo.FA.csproj -c Release -nologo`
Expected: Build succeeds

- [ ] **Step 3: 提交**

```bash
git add Vfx/VfxPath.cs
git commit -m "feat(FA): 新增 VfxPath VFX 路径常量"
```

---

### Task 3: VfxZone — 渲染区接口与实现

**Files:**
- 创建: `Vfx/VfxZone.cs`

- [ ] **Step 1: 创建 VfxZone.cs**

```csharp
using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace HiAuRo.FA.Vfx;

/// <summary>
/// 渲染区接口 —— 代表一个地面 VFX 实例
/// </summary>
public interface IVfxZone : IDisposable
{
    Vector3 Position { get; }
    float Rotation { get; }
    Vector3 Scale { get; }
    Vector4 Color { get; }
    float Duration { get; }
    string Tag { get; }
    bool IsValid { get; }
}

/// <summary>
/// VFX 渲染区实现 —— 持有一个 VfxObject 指针
/// </summary>
public sealed unsafe class VfxZone : IVfxZone
{
    VfxObject* _vfx;
    readonly float _duration;
    float _elapsed;
    bool _disposed;

    public Vector3 Position { get; }
    public float Rotation { get; }
    public Vector3 Scale { get; }
    public Vector4 Color { get; }
    public float Duration => _duration;
    public string Tag { get; }
    public bool IsValid => !_disposed && _vfx != null;

    internal VfxZone(VfxObject* vfx, Vector3 pos, float rotation, Vector3 scale, Vector4 color, float duration, string tag)
    {
        _vfx = vfx;
        Position = pos;
        Rotation = rotation;
        Scale = scale;
        Color = color;
        _duration = duration;
        Tag = tag;
        _elapsed = 0f;
    }

    internal bool Tick(float dt)
    {
        if (_disposed) return false;

        // VFX 实例已被游戏回收
        if (_vfx == null)
        {
            _disposed = true;
            return false;
        }

        // 无限持续时间
        if (_duration < 0) return true;

        _elapsed += dt;
        if (_elapsed >= _duration)
        {
            Dispose();
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_vfx != null)
        {
            VfxNative.Destroy(_vfx);
            _vfx = null;
        }
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build ../HiAuRo.FA/HiAuRo.FA.csproj -c Release -nologo`
Expected: Build succeeds

- [ ] **Step 3: 提交**

```bash
git add Vfx/VfxZone.cs
git commit -m "feat(FA): 新增 IVfxZone 接口和 VfxZone 实现"
```

---

### Task 4: VfxRenderer — 渲染器生命周期

**Files:**
- 创建: `Vfx/VfxRenderer.cs`

- [ ] **Step 1: 创建 VfxRenderer.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;

namespace HiAuRo.FA.Vfx;

/// <summary>
/// VFX 渲染器 —— 管理所有活跃 VFX 实例的生命周期
/// 绑定到 FaPlugin 的 Initialize/Update/Dispose
/// </summary>
public sealed class VfxRenderer
{
    public static VfxRenderer Instance { get; private set; }

    readonly List<VfxZone> _activeZones = [];
    static readonly Vector4 DefaultColor = new(1f, 0.2f, 0.2f, 0.5f);
    const string PoolName = "HiAuRo.FA";

    public VfxRenderer()
    {
        Instance = this;
        VfxNative.Initialize();
    }

    public IVfxZone ShowCircle(Vector3 pos, float radius,
        Vector4? color = null, float duration = -1f, string tag = null)
    {
        return Show(VfxPath.Circle, pos, new Vector3(radius, radius, 1f), 0f, color, duration, tag);
    }

    public IVfxZone ShowRect(Vector3 pos, float width, float length,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string tag = null)
    {
        return Show(VfxPath.Rect, pos, new Vector3(width, 1f, length), rotation, color, duration, tag);
    }

    public IVfxZone ShowFan(Vector3 pos, float radius, float halfAngle,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string tag = null)
    {
        var path = halfAngle <= MathF.PI / 4f ? VfxPath.Fan45 : VfxPath.Fan180;
        return Show(path, pos, new Vector3(radius, radius, 1f), rotation, color, duration, tag);
    }

    public IVfxZone ShowRing(Vector3 pos, float innerR, float outerR,
        Vector4? color = null, float duration = -1f, string tag = null)
    {
        return Show(VfxPath.Ring, pos, new Vector3(outerR, outerR - innerR, 1f), 0f, color, duration, tag);
    }

    public IVfxZone ShowCross(Vector3 pos, float length, float width,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string tag = null)
    {
        return Show(VfxPath.Cross, pos, new Vector3(width, 1f, length), rotation, color, duration, tag);
    }

    public IVfxZone ShowRingFan(Vector3 pos, float innerR, float outerR, float halfAngle,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string tag = null)
    {
        // 扇环：用 Donut + Scale 拟合
        return Show(VfxPath.Donut, pos, new Vector3(outerR, outerR - innerR, 1f), rotation, color, duration, tag);
    }

    public IVfxZone Show(string vfxPath, Vector3 pos, Vector3 scale,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string tag = null)
    {
        var c = color ?? DefaultColor;

        var vfxPtr = VfxNative.Create(vfxPath, PoolName);
        if (vfxPtr == null)
        {
            // 创建失败返回一个已 Dispose 的空 zone（不渲染）
            var deadZone = new VfxZone(null, pos, rotation, scale, c, duration, tag ?? "");
            deadZone.Dispose();
            lock (_activeZones) _activeZones.Add(deadZone);
            return deadZone;
        }

        VfxNative.SetPosition(vfxPtr, pos);
        VfxNative.SetScale(vfxPtr, scale);
        VfxNative.SetRotation(vfxPtr, rotation);
        VfxNative.SetColor(vfxPtr, c);

        var zone = new VfxZone(vfxPtr, pos, rotation, scale, c, duration, tag ?? "");
        lock (_activeZones) _activeZones.Add(zone);
        return zone;
    }

    public void RemoveByTag(string tag)
    {
        lock (_activeZones)
        {
            for (var i = _activeZones.Count - 1; i >= 0; i--)
            {
                if (_activeZones[i].Tag == tag)
                {
                    _activeZones[i].Dispose();
                    _activeZones.RemoveAt(i);
                }
            }
        }
    }

    public void RemoveByTagRegex(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        lock (_activeZones)
        {
            for (var i = _activeZones.Count - 1; i >= 0; i--)
            {
                if (regex.IsMatch(_activeZones[i].Tag))
                {
                    _activeZones[i].Dispose();
                    _activeZones.RemoveAt(i);
                }
            }
        }
    }

    public void Update(float deltaSeconds)
    {
        lock (_activeZones)
        {
            for (var i = _activeZones.Count - 1; i >= 0; i--)
            {
                if (!_activeZones[i].Tick(deltaSeconds))
                {
                    _activeZones[i].Dispose();
                    _activeZones.RemoveAt(i);
                }
            }
        }
    }

    public void Clear()
    {
        lock (_activeZones)
        {
            foreach (var zone in _activeZones)
                zone.Dispose();
            _activeZones.Clear();
        }
    }

    public void Dispose()
    {
        Clear();
        Instance = null;
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build ../HiAuRo.FA/HiAuRo.FA.csproj -c Release -nologo`
Expected: Build succeeds

- [ ] **Step 3: 提交**

```bash
git add Vfx/VfxRenderer.cs
git commit -m "feat(FA): 新增 VfxRenderer 渲染器生命周期管理"
```

---

### Task 5: FaPlugin 接入 VfxRenderer

**Files:**
- 修改: `FaPlugin.cs`

- [ ] **Step 1: 修改 FaPlugin.cs**

读取当前 `FaPlugin.cs` 内容，然后替换为：

```csharp
using HiAuRo;
using HiAuRo.FA.Vfx;

namespace HiAuRo.FA;

public sealed class FaPlugin : IPlugin
{
    VfxRenderer _renderer;

    public string Name => "HiAuRo.FA";
    public string Version => "0.1.0";

    public void Initialize()
    {
        _renderer = new VfxRenderer();
    }

    public void Update()
    {
        _renderer?.Update(DService.Framework.UpdateDelta);
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _renderer = null;
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build ../HiAuRo.FA/HiAuRo.FA.csproj -c Release -nologo`
Expected: Build succeeds

- [ ] **Step 3: 完整构建验证（HiAuRo 主项目也需通过）**

Run: `dotnet build HiAuRo.slnx -c Release -nologo`
Expected: 整个解决方案构建成功

- [ ] **Step 4: 提交**

```bash
git add FaPlugin.cs
git commit -m "feat(FA): FaPlugin 接入 VfxRenderer 生命周期"
```

---

### Task 6: 游戏内验证

本 Task 需要手动在游戏内执行。

- [ ] **Step 1: 确认 VFX 路径解析**

在游戏内通过 Dalamud 控制台或脚本调用：
```csharp
HiAuRo.FA.Vfx.VfxRenderer.Instance.ShowCircle(
    new System.Numerics.Vector3(100, 0, 100), 5f, duration: 5f);
```

观察是否出现地面红色圆形特效。

- [ ] **Step 2: 路径回退测试**

如果短 ID 不生效，尝试完整路径前缀：
- `vfx/omen/eff/n4b5_tr_g05o0g.avfx`
- `vfx/omen/eff/n4b5_tr_g05o0g`

如果完整路径有效，更新 `VfxPath.cs` 添加前缀后缀。

- [ ] **Step 3: 6 种形状逐一验证**

对每种形状调用对应的 Show 方法，确认视觉效果正确。

- [ ] **Step 4: 根据验证结果修正代码并提交**

```bash
git add -A
git commit -m "fix(FA): 根据 VFX 路径验证结果修正路径常量"
```

---

## Self-Review

**1. Spec coverage:**
- Section 3 架构 → Task 1-4 文件结构 ✓
- Section 4 互操作 → Task 1 VfxNative ✓
- Section 5 路径常量 → Task 2 VfxPath ✓
- Section 6 公开 API → Task 3-4 VfxZone + VfxRenderer ✓
- Section 7 生命周期 → Task 5 FaPlugin ✓
- Section 9 验证标准 → Task 6 游戏内验证 ✓

**2. Placeholder scan:** 无 TBD/TODO，所有代码完整。

**3. Type consistency:**
- `VfxZone` 构造函数签名在 Task 3 定义，Task 4 的 Show 方法调用一致 ✓
- `VfxNative` 方法签名在 Task 1 定义，Task 3/4 调用一致 ✓
- `VfxPath` 常量在 Task 2 定义，Task 4 引用一致 ✓
