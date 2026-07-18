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
            // Blur disabled: override to opaque, use theme background
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            Background = TryGetBrush("SystemControlBackgroundAltHighBrush");
            return;
        }

        // Blur enabled: apply selected mode; Background stays
        // as DynamicResource (set by global style in Styles.axaml)
        var levels = prefs.BlurMode switch
        {
            "Mica" => new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur },
            "None" => new[] { WindowTransparencyLevel.None },
            _      => new[] { WindowTransparencyLevel.AcrylicBlur },
        };
        TransparencyLevelHint = levels;
    }

    private static IBrush? TryGetBrush(string key)
    {
        try { return Application.Current?.Resources[key] as IBrush; }
        catch { return null; }
    }
}
