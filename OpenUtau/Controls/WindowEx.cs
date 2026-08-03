using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SukiUI.Controls;

namespace OpenUtau.App.Controls;

/// <summary>
/// Window base class. 继承 SukiWindow：SukiUI 自绘标题栏 + 渐变背景（跟随主题色）。
/// 阶段 E：窗口收敛——
/// - 圆角外透明（OS 透明合成）+ **DWM 边框三属性移除**：关系统圆角
///   （DWMWA_WINDOW_CORNER_PREFERENCE）、关非客户区边框渲染（DWMWA_NCRENDERING_POLICY）、
///   边框颜色透明（DWMWA_BORDER_COLOR，Win11）
/// - 保留 Suki 内容圆角 16px
/// - 标题栏变薄 20% + 无分界线 + 无底部线
/// - 全屏/最大化：直角 + 不透明
/// </summary>
public class WindowEx : SukiWindow
{
    // DWMWA_WINDOW_CORNER_PREFERENCE = 33；DWMWCP_DONOTROUND = 1
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DONOTROUND = 1;
    // DWMWA_NCRENDERING_POLICY = 2；DWMNCRP_DISABLED = 1（禁用非客户区渲染）
    private const int DWMWA_NCRENDERING_POLICY = 2;
    private const int DWMNCRP_DISABLED = 1;
    // DWMWA_BORDER_COLOR = 34（Win11 22000+）；透明 COLORREF
    private const int DWMWA_BORDER_COLOR = 34;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public WindowEx()
    {
        // 阶段 E：圆角外透明（OS 透明合成，露出桌面）
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        // 阶段 E：标题栏收敛——变薄 20%（TitleBarControlSize 10→8）+ 无分界线 + 无底部线
        TitleBarControlSize = 8;
        ShowTitlebarBackground = false;
        ShowBottomBorder = false;
        // 阶段 E：背景混合 GradientDarker（选型确认 2026-08-03：对比预览 5 种后选定）
        BackgroundStyle = SukiUI.Enums.SukiBackgroundStyle.GradientDarker;
        // 阶段 E：四角圆角（Suki 默认 20 的 80%）
        RootCornerRadius = new CornerRadius(16);
        // 圆角外完全透明
        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(0);
    }

    protected override void OnOpened(EventArgs e) {
        base.OnOpened(e);
        ApplyDwmBorderRemoval();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty) {
            // 全屏/窗口化全屏（最大化）：直角 + 不透明；常规：圆角 + 透明
            bool fullscreen = WindowState == WindowState.FullScreen || WindowState == WindowState.Maximized;
            RootCornerRadius = fullscreen ? new CornerRadius(0) : new CornerRadius(16);
            TransparencyLevelHint = fullscreen
                ? new[] { WindowTransparencyLevel.None }
                : new[] { WindowTransparencyLevel.Transparent };
            // 合成模式/状态切换后 DWM 属性可能被重置——重新应用
            if (IsVisible) {
                Dispatcher.UIThread.Post(ApplyDwmBorderRemoval);
            }
        }
    }

    /// <summary>移除 DWM 边框：关系统圆角 + 关非客户区渲染 + 边框颜色透明。</summary>
    private void ApplyDwmBorderRemoval() {
        if (TryGetPlatformHandle() is not { } handle) {
            return;
        }
        try {
            int donotround = DWMWCP_DONOTROUND;
            DwmSetWindowAttribute(handle.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref donotround, sizeof(int));
            int ncdisabled = DWMNCRP_DISABLED;
            DwmSetWindowAttribute(handle.Handle, DWMWA_NCRENDERING_POLICY, ref ncdisabled, sizeof(int));
            int transparentBorder = 0x00000000; // 透明 COLORREF（Win11 边框颜色）
            DwmSetWindowAttribute(handle.Handle, DWMWA_BORDER_COLOR, ref transparentBorder, sizeof(int));
        } catch (Exception) {
            // 非 Windows 或 dwmapi 不可用时忽略
        }
    }
}
