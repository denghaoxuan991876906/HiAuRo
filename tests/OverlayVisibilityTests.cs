#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.UI;

Test_未加载Acr时_不应显示任何Overlay();
Test_已加载Acr且全局开启时_应按面板开关显示();
Console.WriteLine("=== 全部通过 ===");

void Test_未加载Acr时_不应显示任何Overlay()
{
    if (UIManager.ShouldShowStatusBar(allOverlaysVisible: true, hasLoadedAcr: false))
        throw new Exception("未加载 ACR 时不应显示状态栏 overlay");

    if (UIManager.ShouldShowPanel(allOverlaysVisible: true, hasLoadedAcr: false, panelVisible: true))
        throw new Exception("未加载 ACR 时不应显示子面板 overlay");
}

void Test_已加载Acr且全局开启时_应按面板开关显示()
{
    if (!UIManager.ShouldShowStatusBar(allOverlaysVisible: true, hasLoadedAcr: true))
        throw new Exception("已加载 ACR 且全局开启时应显示状态栏 overlay");

    if (!UIManager.ShouldShowPanel(allOverlaysVisible: true, hasLoadedAcr: true, panelVisible: true))
        throw new Exception("已加载 ACR 且面板开启时应显示子面板 overlay");

    if (UIManager.ShouldShowPanel(allOverlaysVisible: true, hasLoadedAcr: true, panelVisible: false))
        throw new Exception("面板单独关闭时不应显示子面板 overlay");
}
