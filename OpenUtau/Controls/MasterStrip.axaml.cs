using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using OpenUtau.Core;

namespace OpenUtau.App.Controls;

/// <summary>
/// 混音台主推子条（E5）：LED 电平表 + 名称 + 静音 + 推子 + 数值。
/// 音量经 PlaybackManager.ApplyMasterVolume 实时作用于 masterMix（MasterAdapter.Scale）。
/// </summary>
public partial class MasterStrip : UserControl {
    static readonly IBrush LedGreen = new SolidColorBrush(Color.FromRgb(39, 174, 96));
    static readonly IBrush LedYellow = new SolidColorBrush(Color.FromRgb(251, 192, 45));
    static readonly IBrush LedRed = new SolidColorBrush(Color.FromRgb(229, 57, 53));
    private Border[] MeterSegments = Array.Empty<Border>();
    private double currentSeg;
    private bool isDragging;
    private double masterDb = 0;

    private const double FaderMin = -24, FaderMax = 12, FaderRange = FaderMax - FaderMin;

    public MasterStrip() {
        InitializeComponent();
        BuildMeterSegments();
        FaderBox.SizeChanged += (s, e) => UpdateFaderPosition();
        FaderBox.AddHandler(PointerReleasedEvent, OnFaderReleased,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        FaderBox.AddHandler(PointerCaptureLostEvent, OnFaderCaptureLost,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        UpdateMuteBtn();
        UpdateFaderPosition();
        UpdateVolValueDisplay(0);
    }

    private void BuildMeterSegments() {
        MeterSegments = new Border[10];
        for (int i = 0; i < 10; i++) {
            var seg = new Border {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = i < 6 ? LedGreen : i < 8 ? LedYellow : LedRed,
                Opacity = 0.18,
            };
            LevelMeterPanel.Children.Add(seg);
            MeterSegments[i] = seg;
        }
    }

    /// <summary>主输出电平（TrackLevels.ReadMasterAndReset 数据源，33ms 轮询）。</summary>
    public void UpdateLevel(float rawPeakDb) {
        float effectiveDb = Math.Clamp(rawPeakDb, -60f, 0f);
        double ratio = Math.Clamp((effectiveDb + 60) / 60.0, 0, 1);
        double targetSeg = ratio * MeterSegments.Length;
        currentSeg = targetSeg >= currentSeg ? targetSeg : Math.Max(targetSeg, currentSeg - 0.67);
        int lit = (int)Math.Ceiling(currentSeg);
        for (int i = 0; i < MeterSegments.Length; i++) {
            MeterSegments[i].Opacity = i < lit ? 1.0 : 0.18;
        }
    }

    // ── Fader ───────────────────────────────────────────

    private double DbToTop(double db) {
        double ratio = 1.0 - Math.Clamp((db - FaderMin) / FaderRange, 0, 1);
        return ratio * (FaderBox.Bounds.Height - ThumbBar.Height);
    }
    private double YToDb(double y) {
        double ratio = 1.0 - Math.Clamp(y / FaderBox.Bounds.Height, 0, 1);
        return FaderMin + ratio * FaderRange;
    }
    private void UpdateFaderPosition() {
        if (FaderBox.Bounds.Height <= 0) return;
        double db = PlaybackManager.Inst.MasterMuted ? FaderMin : masterDb;
        ThumbBar.Margin = new Thickness(0, DbToTop(Math.Clamp(db, FaderMin, FaderMax)), 0, 0);
    }
    private void UpdateVolValueDisplay(double db) {
        VolValueLabel.Text = db <= -24 ? "-∞ dB" : $"{db:+0.0;-0.0} dB";
    }
    private void UpdateMuteBtn() {
        if (PlaybackManager.Inst.MasterMuted) MuteBtn.Classes.Add("muteOn");
        else MuteBtn.Classes.Remove("muteOn");
    }

    private void ApplyMasterVolume(double db) {
        db = Math.Clamp(db, FaderMin, FaderMax);
        masterDb = db;
        PlaybackManager.Inst.ApplyMasterVolume(db);
        DocManager.Inst.ExecuteCmd(new MasterVolumeChangeNotification(db));
        UpdateFaderPosition();
        UpdateVolValueDisplay(db);
    }

    // ── Mouse ───────────────────────────────────────────

    private void OnFaderPressed(object? sender, PointerPressedEventArgs e) {
        isDragging = true;
        e.Pointer.Capture(FaderBox);
        ApplyMasterVolume(YToDb(e.GetPosition(FaderBox).Y));
        e.Handled = true;
    }
    private void OnFaderMoved(object? sender, PointerEventArgs e) {
        if (!isDragging) return;
        ApplyMasterVolume(YToDb(e.GetPosition(FaderBox).Y));
        e.Handled = true;
    }
    private void OnFaderReleased(object? sender, PointerEventArgs e) {
        isDragging = false;
        e.Pointer.Capture(null);
    }
    private void OnFaderCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
        isDragging = false;
    }

    // ── Buttons ─────────────────────────────────────────

    private void OnMuteClick(object? sender, RoutedEventArgs e) {
        PlaybackManager.Inst.SetMasterMuted(!PlaybackManager.Inst.MasterMuted);
        UpdateMuteBtn();
        UpdateFaderPosition();
    }
}
