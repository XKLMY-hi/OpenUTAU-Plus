using Avalonia.Controls;

namespace OpenUtau.App.Controls;

/// <summary>
/// Window base class that enables custom-drawn window chrome
/// (title bar via WindowTitleBar + resize border).
/// Acrylic/Mica transparency is handled globally by Styles.axaml.
/// </summary>
public class WindowEx : Window
{
    public WindowEx() : this(enableCustomChrome: true) { }

    public WindowEx(bool enableCustomChrome)
    {
        if (enableCustomChrome)
        {
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 32;
            SystemDecorations = SystemDecorations.BorderOnly;
        }
    }
}
