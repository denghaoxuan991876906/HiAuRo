using System.Numerics;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Script;
using HiAuRo.UI;

namespace HiAuRo.UI.Tabs;

public sealed class VfxTabPage : TabPageBase
{
    private VfxDebugUI? _vfxDebugUI;

    public VfxTabPage() : base("VFX测试", "vfx_test", IconHelper.Icons.Bug) { }

    public override void DrawContent()
    {
        _vfxDebugUI ??= new VfxDebugUI();
        _vfxDebugUI.Draw();
    }
}
