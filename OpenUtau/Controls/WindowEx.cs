using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace OpenUtau.App.Controls;

/// <summary>
/// Window base class with custom-drawn window chrome (title bar + resize border).
/// 背景固定不透明，由全局 Style 提供跟随主题色渐变画刷（PlusBrushWindowBackground）。
/// </summary>
public class WindowEx : Window
{
    private readonly bool _enableCustomChrome;

    public WindowEx() : this(enableCustomChrome: true) { }

    public WindowEx(bool enableCustomChrome)
    {
        _enableCustomChrome = enableCustomChrome;
        // ── Custom chrome (title bar + resize border) ────
        if (enableCustomChrome)
        {
            // Avalonia 12 官方规范：WindowDrawnDecorations ControlTheme
            // （Plus.Window.axaml）统一自绘边框/标题栏/按钮/拖拽。
            // 注意：WindowDecorations 决定装饰部件启停——None 全部禁用（厚度归零致内容错位），
            // Full + ExtendClientArea=true 走 drawn decorations 模板渲染全部部件
            ExtendClientAreaToDecorationsHint = true;
            WindowDecorations = Avalonia.Controls.WindowDecorations.Full;
        }

        // ── 固定不透明：窗口背景走 PlusBrushWindowBackground 渐变画刷（随主题色变化）──
        TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (!_enableCustomChrome || Presenter is not { } presenter)
        {
            return;
        }
        // 无装饰窗口（WindowDecorations=None，如 SplashWindow）无装饰区可避让，跳过
        if (WindowDecorations != Avalonia.Controls.WindowDecorations.Full)
        {
            return;
        }
        // 自绘装饰（ExtendClientArea=true）下内容扩展进装饰区，须避开标题栏/边框/阴影。
        // WindowDecorationMargin 随窗口状态自动变化（最大化时归零），所有窗口统一生效
        this.GetObservable(WindowDecorationMarginProperty)
            .Subscribe(margin => presenter.Margin = margin);
    }
}
