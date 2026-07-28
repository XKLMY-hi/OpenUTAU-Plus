using System;
using OpenUtau.App.Controls;
using OpenUtau.Core.Util;

namespace OpenUtau.App.Views;

public partial class MixerWindow : WindowEx
{
    private MixerControl? _mixerControl;
    private bool _forceClose;

    public MixerWindow() { InitializeComponent(); }

    public MixerWindow(MixerControl mixerControl) : this()
    {
        _mixerControl = mixerControl;
        MixerContainer.Content = mixerControl;

        // Restore window position
        if (Preferences.Default.MixerWindowSize.TryGetPosition(out int x, out int y))
            Position = new Avalonia.PixelPoint(x, y);
        WindowState = (Avalonia.Controls.WindowState)Preferences.Default.MixerWindowSize.State;

        Closing += OnWindowClosing;
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save position
        Preferences.Default.MixerWindowSize.Set(Width, Height, Position.X, Position.Y, (int)WindowState);
        Preferences.Save();

        if (!_forceClose)
        {
            // Hide rather than close – control will be reparented
            e.Cancel = true;
            Hide();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_forceClose && _mixerControl != null)
        {
            _mixerControl.Shutdown();
            _mixerControl = null;
        }
    }
}
