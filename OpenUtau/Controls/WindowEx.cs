using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace OpenUtau.App.Controls;

/// <summary>
/// Window base class with custom-drawn window chrome (title bar + resize border)
/// and configurable transparency (AcrylicBlur / Mica / None) via Preferences.
/// </summary>
public class WindowEx : Window
{
    private IDisposable? _themeSub;

    public WindowEx() : this(enableCustomChrome: true) { }

    public WindowEx(bool enableCustomChrome)
    {
        // ── Custom chrome (title bar + resize border) ────
        if (enableCustomChrome)
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 32;
            SystemDecorations = SystemDecorations.BorderOnly;
        }

        // ── Blur / transparency (driven by Preferences) ──
        ApplyBlurSettings();
        // 主题切换时重应用模糊/背景（背景画刷是静态实例，需随主题刷新，避免"切换不完全"）
        _themeSub = MessageBus.Current.Listen<ThemeChangedEvent>()
            .Subscribe(_ => ApplyBlurSettings());
        Closed += (_, _) => _themeSub?.Dispose();
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
