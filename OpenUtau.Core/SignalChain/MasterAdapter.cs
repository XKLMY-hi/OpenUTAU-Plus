using System;
using NAudio.Wave;

namespace OpenUtau.Core.SignalChain {
    public class MasterAdapter : ISampleProvider {
        private readonly WaveFormat waveFormat;
        private readonly ISignalSource source;
        private int position;

        public WaveFormat WaveFormat => waveFormat;
        public int Waited { get; private set; }
        public bool IsWaiting { get; private set; }
        /// <summary>主音量增益（混音台主推子，1.0 = 原声）。</summary>
        public double Scale { get; set; } = 1.0;

        private float peak;
        public MasterAdapter(ISignalSource source) {
            this.source = source;
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.SampleRate, source.Channels);
        }

        public int Read(float[] buffer, int offset, int count) {
            for (int i = offset; i < offset + count; ++i) {
                buffer[i] = 0;
            }
            if (!source.IsReady(position, count)) {
                Waited += count;
                IsWaiting = true;
                // 频谱 tap：buffer 已在函数开头清零 → 频谱自然衰减归零
                SpectrumBus.Inst.AddSamples(buffer, offset, count, waveFormat.Channels);
                return count;
            } else {
                int pos = source.Mix(position, buffer, offset, count);
                int n = Math.Max(0, pos - position);
                position = pos;
                IsWaiting = false;
                if (Scale != 1.0) {
                    for (int i = offset; i < offset + count; ++i) {
                        buffer[i] = (float)(buffer[i] * Scale);
                    }
                }
                // 峰值统计（Scale 之后 = 实际可听输出）
                float max = 0;
                for (int i = offset; i < offset + count; ++i) {
                    float v = Math.Abs(buffer[i]);
                    if (v > max) max = v;
                }
                if (max > peak) peak = max;
                // 频谱 tap：Scale 之后 = 最终可听混音（含效果器链）；用 n（实际有数据的长度）
                SpectrumBus.Inst.AddSamples(buffer, offset, n, waveFormat.Channels);
                return n;
            }
        }

        /// <summary>读取主输出峰值 dB（-60..0）并清零。</summary>
        public float ReadAndResetPeakDb() {
            float p = peak;
            peak = 0;
            if (p <= 0.0001f) {
                return -60f;
            }
            return Math.Clamp(20f * (float)Math.Log10(p), -60f, 0f);
        }

        public void SetPosition(int position) {
            this.position = position;
            Waited = 0;
            // Reset time-domain state on seek (default no-op for non-EffectChain sources)
            source.Reset();
        }
    }
}
