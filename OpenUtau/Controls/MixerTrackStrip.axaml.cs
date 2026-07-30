using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using System.Reactive.Linq;
using ReactiveUI;

using static OpenUtau.Core.DocManager;

namespace OpenUtau.App.Controls {
    public partial class MixerTrackStrip : UserControl {
        private UTrack? track;
        private bool isDragging;
        private bool suppressingEvents;
        private bool _syncing;
        private IDisposable? _volumeSubscription;
        private IDisposable? _panSubscription;
        private ViewModels.MixerTrackStripViewModel? _vm;
        public UTrack? Track {
            get => track;
            set { if (track == value) return; track = value; _vm = track != null ? new ViewModels.MixerTrackStripViewModel(track) : null; LoadTrackData(); }
        }
        public int TrackIndex { get; set; } = -1;

        public MixerTrackStrip() {
            InitializeComponent();
            FaderBox.SizeChanged += (s, e) => UpdateFaderPosition();
            FaderBox.AddHandler(PointerReleasedEvent, OnFaderReleased,
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
            FaderBox.AddHandler(PointerCaptureLostEvent, OnFaderCaptureLost,
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        }
        public MixerTrackStrip(UTrack track) : this() {
            Track = track;
            // Subscribe to volume/pan changes from main window
            _volumeSubscription = MessageBus.Current.Listen<VolumeChangeNotification>()
                .Where(n => n.TrackNo == track.TrackNo)
                .Subscribe(n => {
                    if (_syncing) return;
                    _syncing = true;
                    double db = Math.Clamp(n.Volume, FaderMin, FaderMax);
                    UpdateFaderPositionFromDb(db);
                    UpdateVolValueDisplay(db);
                    _syncing = false;
                });
            _panSubscription = MessageBus.Current.Listen<PanChangeNotification>()
                .Where(n => n.TrackNo == track.TrackNo)
                .Subscribe(n => {
                    if (_syncing) return;
                    _syncing = true;
                    suppressingEvents = true;
                    PanSlider.Value = Math.Clamp(n.Pan, -100, 100);
                    track.Pan = n.Pan / 100.0;
                    suppressingEvents = false;
                    _syncing = false;
                });
            Unloaded += OnStripUnloaded;
        }

        private void OnStripUnloaded(object? sender, EventArgs e) => DisposeSubscriptions();

        public void DisposeSubscriptions() {
            _volumeSubscription?.Dispose();
            _volumeSubscription = null;
            _panSubscription?.Dispose();
            _panSubscription = null;
            PanSlider.PropertyChanged -= OnPanSliderValueChanged;
            _vm = null;
        }

        private void OnPanSliderValueChanged(object? s, Avalonia.AvaloniaPropertyChangedEventArgs e) {
            if (e.Property == RangeBase.ValueProperty && !suppressingEvents && track != null && _vm != null) {
                _vm.ApplyPan(PanSlider.Value);
                var pn = new PanChangeNotification(track.TrackNo, PanSlider.Value);
                DocManager.Inst.ExecuteCmd(pn);
                MessageBus.Current.SendMessage(pn);
            }
        }

        private void LoadTrackData() {
            if (track == null || _vm == null) return;
            _vm.Refresh();
            suppressingEvents = true;
            TrackNameLabel.Text = _vm.TrackName;
            ColorBar.Background = _vm.TrackColor;
            UpdateMuteSoloButtons();
            PanSlider.Value = _vm.Pan;
            PanSlider.PropertyChanged -= OnPanSliderValueChanged;
            PanSlider.PropertyChanged += OnPanSliderValueChanged;
            UpdateVolValueDisplay(Math.Clamp(_vm.Volume, FaderMin, FaderMax));
            UpdateFaderPosition();
            UpdateFxEntryBtn();
            suppressingEvents = false;
        }

        private void UpdateMuteSoloButtons() {
            if (track == null) return;
            if (track.Mute) MuteBtn.Classes.Add("muteOn"); else MuteBtn.Classes.Remove("muteOn");
            if (track.Solo) SoloBtn.Classes.Add("soloOn"); else SoloBtn.Classes.Remove("soloOn");
        }
        private void UpdateVolValueDisplay(double db) {
            VolValueLabel.Text = db <= -24 ? "-∞ dB" : $"{db:+0.0;-0.0} dB";
        }
        private void UpdateFxEntryBtn() {
            if (track?.MixFx?.Enabled == true)
                FxEntryBtn.Classes.Add("fxActive");
            else
                FxEntryBtn.Classes.Remove("fxActive");
        }

        // ── Fader ───────────────────────────────────────────
        private const double FaderMin = -24, FaderMax = 12, FaderRange = FaderMax - FaderMin;
        private double DbToTop(double db) {
            double ratio = 1.0 - Math.Clamp((db - FaderMin) / FaderRange, 0, 1);
            return ratio * (FaderBox.Bounds.Height - ThumbBar.Height);
        }
        private double YToDb(double y) {
            double ratio = 1.0 - Math.Clamp(y / FaderBox.Bounds.Height, 0, 1);
            return FaderMin + ratio * FaderRange;
        }
        private void UpdateFaderPositionFromDb(double db) {
            if (track == null || FaderBox.Bounds.Height <= 0) return;
            ThumbBar.Margin = new Thickness(-2, DbToTop(Math.Clamp(db, FaderMin, FaderMax)), -2, 0);
        }

        private void UpdateFaderPosition() {
            if (track == null || FaderBox.Bounds.Height <= 0) return;
            double db = Math.Clamp(track.Volume, FaderMin, FaderMax);
            ThumbBar.Margin = new Thickness(-2, DbToTop(db), -2, 0);
        }
        private static IBrush GetLevelBrush(double ratio) {
            byte r, g, b; const byte a = 100;
            if (ratio < 0.6) { double t = ratio / 0.6; r = (byte)(39 + 202 * t); g = (byte)(174 + 22 * t); b = (byte)(96 - 96 * t); }
            else if (ratio < 0.85) { double t = (ratio - 0.6) / 0.25; r = (byte)(241 - 10 * t); g = (byte)(196 - 120 * t); b = 0; }
            else { double t = (ratio - 0.85) / 0.15; r = 231; g = (byte)(76 * (1 - t) + 39 * t); b = 0; }
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }
        private void ApplyVolume(double db) {
            if (track == null || _vm == null) return;
            db = Math.Clamp(db, FaderMin, FaderMax);
            _vm.ApplyVolume(db);
            var vn = new VolumeChangeNotification(track.TrackNo, track.Muted ? -24 : db);
            DocManager.Inst.ExecuteCmd(vn);
            MessageBus.Current.SendMessage(vn);
            UpdateFaderPosition();
            UpdateVolValueDisplay(db);
        }

        // ── Mouse ───────────────────────────────────────────
        private void OnFaderPressed(object? sender, PointerPressedEventArgs e) {
            if (track == null) return;
            isDragging = true;
            e.Pointer.Capture(FaderBox);
            ApplyVolume(YToDb(e.GetPosition(FaderBox).Y));
            e.Handled = true;
        }
        private void OnFaderMoved(object? sender, PointerEventArgs e) {
            if (!isDragging || track == null) return;
            ApplyVolume(YToDb(e.GetPosition(FaderBox).Y));
            e.Handled = true;
        }
        private void OnFaderReleased(object? sender, PointerEventArgs e) {
            isDragging = false; e.Pointer.Capture(null);
        }
        private void OnFaderCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
            isDragging = false;
        }

        private void OnVolLabelPressed(object? sender, PointerPressedEventArgs e) {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.ClickCount == 2)
                BeginEditVolume();
        }
        private void BeginEditVolume() {
            if (track == null) return;
            VolValueLabel.IsVisible = false;
            var tb = new TextBox {
                Text = $"{track.Volume:F1}", FontSize = 11, FontFamily = "monospace",
                TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(8, 2),
            };
            var parent = VolValueLabel.Parent as Panel;
            int idx = parent?.Children.IndexOf(VolValueLabel) ?? -1;
            if (parent != null && idx >= 0) {
                parent.Children.Insert(idx, tb);
                tb.SelectAll(); tb.Focus();
                tb.KeyDown += (s, e) => {
                    if (e.Key == Key.Enter) CommitEdit(tb);
                    else if (e.Key == Key.Escape) CancelEdit(tb);
                };
                tb.LostFocus += (s, e) => CommitEdit(tb);
            }
        }
        private void CommitEdit(TextBox tb) {
            if (double.TryParse(tb.Text, out double db)) ApplyVolume(Math.Clamp(db, FaderMin, FaderMax));
            CancelEdit(tb);
        }
        private void CancelEdit(TextBox tb) { (tb.Parent as Panel)?.Children.Remove(tb); VolValueLabel.IsVisible = true; }

        // ── Buttons ─────────────────────────────────────────
        private void OnMuteClick(object? sender, RoutedEventArgs e) {
            _vm?.ToggleMuteCmd.Execute(null);
            UpdateMuteSoloButtons();
        }
        private void OnSoloClick(object? sender, RoutedEventArgs e) {
            _vm?.ToggleSoloCmd.Execute(null);
            UpdateMuteSoloButtons();
        }
        private void OnFxEntryClick(object? sender, RoutedEventArgs e) {
            if (track == null) return;
            // Reuse existing window if already open for this track
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) {
                var existing = desktop.Windows.OfType<Views.TrackEffectRack>()
                    .FirstOrDefault(w => w.Tag is UTrack t && t.TrackNo == track.TrackNo);
                if (existing != null) {
                    existing.Activate();
                    return;
                }
            }
            var rack = new Views.TrackEffectRack(track) { Tag = track };
            rack.Closed += (_, __) => Refresh();
            rack.Show();
        }
        // ── Level Meter ────────────────────────────────────
        public void UpdateLevel(float rawPeakDb) {
            if (FaderBox.Bounds.Height <= 0 || _vm == null) return;
            bool silent = _vm.IsSilent;
            float gainDb = silent ? -60f : (float)_vm.Volume;
            float effectiveDb = Math.Clamp(rawPeakDb + gainDb, -60f, 0f);
            double ratio = (effectiveDb + 60) / 60.0;
            double targetH = ratio * FaderBox.Bounds.Height;
            double currentH = LevelFill.Height;
            double decay = FaderBox.Bounds.Height / 15.0;
            double newH = targetH >= currentH ? targetH : Math.Max(targetH, currentH - decay);
            newH = Math.Clamp(newH, 0, FaderBox.Bounds.Height);
            LevelFill.Height = newH;
            LevelFill.Background = GetLevelBrush(Math.Clamp(ratio, 0, 1));
        }

        public void Refresh() => LoadTrackData();
    }
}
