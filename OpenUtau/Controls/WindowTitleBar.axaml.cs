using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace OpenUtau.App.Controls;

public partial class WindowTitleBar : UserControl
{
    public WindowTitleBar()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) =>
        {
            if (VisualRoot is not Window window) return;

            // Sync initial title (icon is set via XAML data binding)
            TitleText.Text = window.Title ?? string.Empty;

            // Track window state → maximize/restore icon
            window.GetObservable(Window.WindowStateProperty)
                  .Subscribe(state => UpdateMaxRestoreIcon(state));

            // Track title changes
            window.GetObservable(Window.TitleProperty)
                  .Subscribe(title => TitleText.Text = title ?? string.Empty);
        };
    }

    private Window? HostWindow => VisualRoot as Window;

    // ── Icon toggle ───────────────────────────────────────

    private void UpdateMaxRestoreIcon(WindowState state)
    {
        if (MaxRestoreIcon is null) return;

        if (state == WindowState.Maximized)
        {
            // restore icon — two overlapping squares
            MaxRestoreIcon.Data = Geometry.Parse("M4 8h4V4h12v12h-4v4H4V8z");
        }
        else
        {
            // maximize icon — single outlined square
            MaxRestoreIcon.Data = Geometry.Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2");
        }
    }

    // ── Drag ──────────────────────────────────────────────

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            HostWindow?.BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (HostWindow is not Window w) return;
        w.WindowState = w.WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;
    }

    // ── Buttons ───────────────────────────────────────────

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (HostWindow is Window w) w.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestore(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (HostWindow is not Window w) return;
        w.WindowState = w.WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HostWindow?.Close();
    }
}
