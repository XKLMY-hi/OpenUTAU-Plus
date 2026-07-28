using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.Util;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Controls;

public partial class MixerControl : UserControl
{
    internal readonly MixerViewModel ViewModel;
    private readonly DispatcherTimer levelTimer;
    private int _tickCount;

    public MixerControl()
    {
        InitializeComponent();
        DataContext = ViewModel = new MixerViewModel();
        RebuildStrips();
        ViewModel.Tracks.CollectionChanged += OnTracksChanged;

        // Forward space to main window
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                var mainWindow = (Application.Current?.ApplicationLifetime
                    as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                    ?.MainWindow;
                mainWindow?.Focus();
            }
        };

        // Poll track levels at ~30 fps for VU meter animation
        levelTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(33),
            DispatcherPriority.Render,
            OnLevelTimerTick);
        levelTimer.Start();
    }

    private void OnLevelTimerTick(object? sender, EventArgs e)
    {
        _tickCount++;
        if (_tickCount == 1)
            Serilog.Log.Information($"[Mixer] Timer started, {TrackStripsPanel.Children.Count} strips");

        foreach (var child in TrackStripsPanel.Children)
        {
            if (child is MixerTrackStrip strip && strip.Track != null)
            {
                float db = TrackLevels.ReadAndReset(strip.Track.TrackNo);
                strip.UpdateLevel(db);
            }
        }
    }

    private void OnTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildStrips();
    }

    public void RebuildStrips()
    {
        TrackStripsPanel.Children.Clear();
        if (ViewModel.Tracks.Count == 0) return;
        for (int i = 0; i < ViewModel.Tracks.Count; i++)
        {
            var strip = new MixerTrackStrip(ViewModel.Tracks[i]) { TrackIndex = i };
            TrackStripsPanel.Children.Add(strip);
        }
        StatusText.Text = string.Format(ThemeManager.GetString("mixer.tracks"), ViewModel.Tracks.Count);
    }

    public void Shutdown()
    {
        levelTimer.Stop();
        ViewModel.Tracks.CollectionChanged -= OnTracksChanged;
        DocManager.Inst.RemoveSubscriber(ViewModel);
    }
}
