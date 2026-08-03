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

        static readonly IBrush LedGreen = new SolidColorBrush(Color.FromRgb(39, 174, 96));
        static readonly IBrush LedYellow = new SolidColorBrush(Color.FromRgb(251, 192, 45));
        static readonly IBrush LedRed = new SolidColorBrush(Color.FromRgb(229, 57, 53));
        private Border[] MeterSegments = Array.Empty<Border>();
        private double currentSeg;

        public MixerTrackStrip() {
            InitializeComponent();
            BuildMeterSegments();
            FaderBox.SizeChanged += (s, e) => UpdateFaderPosition();
            FaderBox.AddHandler(PointerReleasedEvent, OnFaderReleased,
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
            FaderBox.AddHandler(PointerCaptureLostEvent, OnFaderCaptureLost,
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        }

        /// <summary>LED 电平表：10 段（绿 6 / 黄 2 / 红 2），未点亮段低透明度显示表底。</summary>
        private void BuildMeterSegments() {
            MeterSegments = new Border[10];
            for (int i = 0; i < 10; i++) {
                var seg = new Border {
                    Height = 3,
                    Margin = new Thickness(0),
                    CornerRadius = new CornerRadius(1.5),
                    Background = i < 6 ? LedGreen : i < 8 ? LedYellow : LedRed,
                    Opacity = 0.18,
                };
                LevelMeterPanel.Children.Add(seg);
                MeterSegments[i] = seg;
            }
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
                    PanKnobControl.Value = Math.Clamp(n.Pan, -100, 100);
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
            PanKnobControl.ValueChanged -= OnPanKnobChanged;
            _vm = null;
        }

        private void OnPanKnobChanged(object? s, double pan) {
            if (!suppressingEvents && track != null && _vm != null) {
                _vm.ApplyPan(pan);
                var pn = new PanChangeNotification(track.TrackNo, pan);
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
            PanKnobControl.ValueChanged -= OnPanKnobChanged;
            PanKnobControl.Value = _vm.Pan;
            PanKnobControl.ValueChanged += OnPanKnobChanged;
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
            ThumbBar.Margin = new Thickness(0, DbToTop(Math.Clamp(db, FaderMin, FaderMax)), 0, 0);
        }

        private void UpdateFaderPosition() {
            if (track == null || FaderBox.Bounds.Height <= 0) return;
            double db = Math.Clamp(track.Volume, FaderMin, FaderMax);
            ThumbBar.Margin = new Thickness(0, DbToTop(db), 0, 0);
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
        // ── Level Meter（LED 10 段）────────────────────────
        public void UpdateLevel(float rawPeakDb) {
            if (_vm == null) return;
            bool silent = _vm.IsSilent;
            float gainDb = silent ? -60f : (float)_vm.Volume;
            float effectiveDb = Math.Clamp(rawPeakDb + gainDb, -60f, 0f);
            double ratio = Math.Clamp((effectiveDb + 60) / 60.0, 0, 1);
            double targetSeg = ratio * MeterSegments.Length;
            // 追峰 + 衰减（33ms/tick，10 段约 1.5 tick 衰减满程）
            currentSeg = targetSeg >= currentSeg ? targetSeg : Math.Max(targetSeg, currentSeg - 0.67);
            int lit = (int)Math.Ceiling(currentSeg);
            for (int i = 0; i < MeterSegments.Length; i++) {
                MeterSegments[i].Opacity = i < lit ? 1.0 : 0.18;
            }
        }

        public void Refresh() => LoadTrackData();
    }
}
