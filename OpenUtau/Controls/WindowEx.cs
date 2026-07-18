using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;

namespace OpenUtau.App.Controls;

/// <summary>
/// Enhanced Window base class with acrylic blur / Mica transparency,
/// and optional custom-drawn window chrome (title bar + resize border).
/// </summary>
public class WindowEx : Window
{
    public WindowEx() : this(enableCustomChrome: true) { }

    public WindowEx(bool enableCustomChrome)
    {
        // ── Transparency (acrylic / Mica) ────────────────
        try
        {
            ExtendClientAreaToDecorationsHint = true;
            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Mica,
            };
            Background = Brushes.Transparent;
        }
        catch
        {
            // Platform doesn't support acrylic — fall back silently
        }

        // ── Custom window chrome ──────────────────────────
        if (enableCustomChrome)
        {
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 32;
            SystemDecorations = SystemDecorations.BorderOnly;
        }
    }
}
