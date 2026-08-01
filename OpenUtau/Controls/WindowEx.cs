using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ReactiveUI;

namespace OpenUtau.App.Controls;

/// <summary>
/// Window base class with custom-drawn window chrome (title bar + resize border)
/// and configurable transparency (AcrylicBlur / Mica / None) via Preferences.
/// </summary>
public class WindowEx : Window
{
    private readonly bool _enableCustomChrome;
    private IDisposable? _themeSub;

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

        // ── Blur / transparency (driven by Preferences) ──
        ApplyBlurSettings();
        // 主题切换时重应用模糊/背景（背景画刷是静态实例，需随主题刷新，避免"切换不完全"）
        _themeSub = MessageBus.Current.Listen<ThemeChangedEvent>()
            .Subscribe(_ => ApplyBlurSettings());
        Closed += (_, _) => _themeSub?.Dispose();
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

    private void ApplyBlurSettings()
    {
        var prefs = Core.Util.Preferences.Default;

        if (!prefs.EnableBlur)
        {
            // Blur disabled: override to opaque, use theme background
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            Background = TryGetBrush("SystemControlBackgroundAltHighBrush");
            return;
        }

        // Blur enabled: apply selected mode; Background stays
        // as DynamicResource (set by global style in Styles.axaml)
        var levels = prefs.BlurMode switch
        {
            "Mica" => new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur },
            "None" => new[] { WindowTransparencyLevel.None },
            _      => new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur },
        };
        TransparencyLevelHint = levels;
    }

    private static IBrush? TryGetBrush(string key)
    {
        // 必须用变体感知解析（普通索引器只查根字典直接条目，取不到 merged/ThemeDictionaries 里的画刷）
        try
        {
            if (Application.Current != null &&
                Application.Current.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var value))
            {
                return value as IBrush;
            }
        }
        catch { }
        return null;
    }
}
