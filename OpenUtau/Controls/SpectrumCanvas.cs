using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using NWaves.Transforms;
using OpenUtau.Core;
using OpenUtau.Core.SignalChain;

namespace OpenUtau.App.Controls {
    /// <summary>
    /// 钢琴窗底部频谱条：轮询 <see cref="SpectrumBus"/>（主推子后的最终输出），
    /// Hann + FFT 后画 48 条对数频段长条，随音乐跳动。仅播放时显示（停止即隐藏，
    /// 还原下方波形条）。33ms DispatcherTimer 驱动（与 MixerControl VU 表同型）。
    /// 全程复用预分配缓冲，零 GC 分配。
    /// </summary>
    public class SpectrumCanvas : Control {
        private const int BandCount = 96;                  // 显示长条数（细条）
        private const float FMinHz = 40f;                  // 频段下限（低于此丢弃）
        private const float FMaxHz = 16000f;               // 频段上限
        private const float Release = 0.90f;               // 每 tick 峰值衰减（≈300ms 尾巴）
        private const float DbRange = 45f;                 // 显示动态范围（-45dB..0dB，敏感度优先）

        private readonly float[] _hann = new float[SpectrumBus.FftSize];
        private readonly float[] _windowed = new float[SpectrumBus.FftSize];
        private readonly float[] _re = new float[SpectrumBus.FftSize / 2 + 1];
        private readonly float[] _im = new float[SpectrumBus.FftSize / 2 + 1];
        private readonly float[] _spectrum = new float[SpectrumBus.FftSize / 2 + 1];
        private readonly float[] _smooth = new float[BandCount];
        private readonly int[] _bandBins = new int[BandCount + 1]; // log 频段 bin 边界（预计算）
        private readonly RealFft _fft = new(SpectrumBus.FftSize);
        private readonly IBrush[] _barBrushes = new IBrush[BandCount];

        private DispatcherTimer? _timer;
        private long _lastSeq = -1;
        private bool _gradCached;
        private Color _gradColor;

        // SpectrumBus.Enabled 引用计数——Detached 模式下主窗/独立窗口各一份 canvas，
        // attach/detach 顺序不定，bool 会互相踩，用计数归零才关
        private static int _refCount;

        public SpectrumCanvas() {
            IsHitTestVisible = false;
            // Hann 窗（预计算）
            for (int k = 0; k < SpectrumBus.FftSize; k++) {
                _hann[k] = 0.5f * (1 - MathF.Cos(2 * MathF.PI * k / SpectrumBus.FftSize));
            }
            BuildBandBins();
        }

        // ── 生命周期 ──────────────────────────────────────────

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
            base.OnAttachedToVisualTree(e);
            if (Interlocked.Increment(ref _refCount) == 1) {
                SpectrumBus.Inst.Enabled = true;
            }
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, OnTimerTick);
            _timer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            base.OnDetachedFromVisualTree(e);
            _timer?.Stop();
            _timer = null;
            if (Interlocked.Decrement(ref _refCount) == 0) {
                SpectrumBus.Inst.Enabled = false;
            }
        }

        // ── 定时刷新 ──────────────────────────────────────────

        private void OnTimerTick(object? sender, EventArgs e) {
            bool playing = PlaybackManager.Inst.PlayingMaster;
            if (IsVisible != playing) {
                IsVisible = playing; // 停止即隐藏（还原下方波形条）
            }
            if (!playing) {
                return; // 隐藏期零 FFT 零绘制
            }
            if (!SpectrumBus.Inst.TryGetLatestBlock(out var block, out long seq)) {
                return;
            }
            if (seq == _lastSeq) {
                return; // 无新块跳过 FFT
            }
            _lastSeq = seq;
            UpdateLevels(block!);
            InvalidateVisual();
        }

        // ── 频谱分析（零分配） ────────────────────────────────

        private void BuildBandBins() {
            float sr = AudioSettings.SampleRate;
            for (int i = 0; i <= BandCount; i++) {
                // 对数频段：fLow_i = FMin * (FMax/FMin)^(i/BandCount)
                float fLow = FMinHz * MathF.Pow(FMaxHz / FMinHz, (float)i / BandCount);
                int k = (int)MathF.Ceiling(fLow * SpectrumBus.FftSize / sr);
                _bandBins[i] = Math.Clamp(k, 0, _spectrum.Length - 1);
            }
        }

        private void UpdateLevels(float[] block) {
            // 1. Hann 加窗
            for (int k = 0; k < SpectrumBus.FftSize; k++) {
                _windowed[k] = block[k] * _hann[k];
            }
            // 2. FFT——Direct + 手算幅值（零分配路径，避免 MagnitudeSpectrum 潜在内部分配）
            _fft.Direct(_windowed, _re, _im);
            // 幅度还原因子：Hann 窗相干增益 0.5 → 2/(N*0.5) = 4/N（满幅正弦 → ~1.0）
            float norm = 4f / SpectrumBus.FftSize;
            for (int k = 0; k < _spectrum.Length; k++) {
                _spectrum[k] = MathF.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]) * norm;
            }
            // 3. 对数频段平均 → dB 归一（非线性提升低电平）→ 快起慢落平滑
            for (int i = 0; i < BandCount; i++) {
                int k0 = _bandBins[i], k1 = _bandBins[i + 1];
                float sum = 0;
                for (int k = k0; k < k1; k++) {
                    sum += _spectrum[k];
                }
                float band = sum / Math.Max(1, k1 - k0);
                float db = 20 * MathF.Log10(band + 1e-5f);
                float level = Math.Clamp((db + DbRange) / DbRange, 0f, 1f);
                level = MathF.Pow(level, 0.7f); // 非线性增益：低电平提升，跳动更明显
                _smooth[i] = Math.Max(level, _smooth[i] * Release); // instant attack, 指数 decay
            }
        }

        // ── 绘制 ──────────────────────────────────────────────

        public override void Render(DrawingContext context) {
            RefreshGradBrushesIfThemeChanged();
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) {
                return;
            }
            double barW = (w - (BandCount + 1)) / BandCount; // 1px 间距（细条）
            for (int i = 0; i < BandCount; i++) {
                double bh = Math.Max(1, _smooth[i] * (h - 2)); // 最低 1px 保证弱信号可见
                double x = i * (barW + 1);
                context.DrawRectangle(_barBrushes[i], null, new Rect(x, h - bh, barW, bh));
            }
        }

        /// <summary>主题色渐变（AccentBrush1 → NeutralAccentBrush），O(1) 比较检测主题切换。</summary>
        private void RefreshGradBrushesIfThemeChanged() {
            var accent = ((ISolidColorBrush)ThemeManager.AccentBrush1).Color;
            if (_gradCached && _gradColor == accent) {
                return;
            }
            _gradCached = true;
            _gradColor = accent;
            var baseColor = ((ISolidColorBrush)ThemeManager.NeutralAccentBrush).Color;
            for (int i = 0; i < BandCount; i++) {
                float t = (float)i / (BandCount - 1);
                _barBrushes[i] = new SolidColorBrush(Lerp(baseColor, accent, t));
            }
        }

        private static Color Lerp(Color a, Color b, float t) {
            return new Color(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }
    }
}
