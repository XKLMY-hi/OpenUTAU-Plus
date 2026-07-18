using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OpenUtau.App.Controls;

/// <summary>
/// Window base class with custom-drawn window chrome (title bar + resize border)
/// and configurable transparency (AcrylicBlur / Mica / None) via Preferences.
/// </summary>
public class WindowEx : Window
{
    public WindowEx() : this(enableCustomChrome: true) { }

    public WindowEx(bool enableCustomChrome)
    {
        // ── Custom chrome (title bar + resize border) ────
        if (enableCustomChrome)
        {
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 32;
            SystemDecorations = SystemDecorations.BorderOnly;
        }

        // ── Blur / transparency (driven by Preferences) ──
        ApplyBlurSettings();
    }

    private void ApplyBlurSettings()
    {
        var prefs = Core.Util.Preferences.Default;

        if (!prefs.EnableBlur)
        {
            // Blur disabled: opaque background, no transparency hint
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            ExtendClientAreaToDecorationsHint = true; // keep custom chrome area
            Background = TryGetBrush("SystemControlBackgroundAltHighBrush")
                ?? TryGetBrush("AcrylicTintBrush")
                ?? Brushes.Transparent;
            return;
        }

        // Blur enabled: apply selected mode
        var levels = prefs.BlurMode switch
        {
            "Mica"   => new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur },
            "None"   => new[] { WindowTransparencyLevel.None },
            _        => new[] { WindowTransparencyLevel.AcrylicBlur },
        };
        TransparencyLevelHint = levels;
        ExtendClientAreaToDecorationsHint = true;

        // Use acrylic tint brush (theme-aware); fall back to transparent
        Background = TryGetBrush("AcrylicTintBrush") ?? Brushes.Transparent;
    }

    private static IBrush? TryGetBrush(string key)
    {
        try
        {
            return Application.Current?.Resources[key] as IBrush;
        }
        catch { return null; }
    }
}
