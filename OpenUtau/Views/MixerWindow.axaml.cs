using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OpenUtau.App.Controls;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.Util;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Views {
    public partial class MixerWindow : Window {
        private readonly MixerViewModel viewModel;
        private readonly DispatcherTimer levelTimer;

        public MixerWindow() {
            InitializeComponent();
            DataContext = viewModel = new MixerViewModel();
            RebuildStrips();
            viewModel.Tracks.CollectionChanged += OnTracksChanged;

            // Forward space to main window
            KeyDown += (s, e) => {
                if (e.Key == Key.Space) {
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

        private int _tickCount;
        private void OnLevelTimerTick(object? sender, EventArgs e) {
            _tickCount++;
            if (_tickCount == 1) {
                Serilog.Log.Information($"[Mixer] Timer started, {TrackStripsPanel.Children.Count} strips visible={IsVisible}");
            }
            foreach (var child in TrackStripsPanel.Children) {
                if (child is MixerTrackStrip strip && strip.Track != null) {
                    float db = TrackLevels.ReadAndReset(strip.Track.TrackNo);
                    strip.UpdateLevel(db);
                }
            }
        }

        private void OnTracksChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            RebuildStrips();
        }

        private void RebuildStrips() {
            TrackStripsPanel.Children.Clear();
            if (viewModel.Tracks.Count == 0) return;
            for (int i = 0; i < viewModel.Tracks.Count; i++) {
                var strip = new MixerTrackStrip(viewModel.Tracks[i]) { TrackIndex = i };
                TrackStripsPanel.Children.Add(strip);
            }
            StatusText.Text = string.Format(ThemeManager.GetString("mixer.tracks"), viewModel.Tracks.Count);
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            levelTimer.Stop();
            viewModel.Tracks.CollectionChanged -= OnTracksChanged;
            DocManager.Inst.RemoveSubscriber(viewModel);
        }
    }
}
