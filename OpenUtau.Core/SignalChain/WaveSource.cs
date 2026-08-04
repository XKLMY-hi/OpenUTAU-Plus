using System;

namespace OpenUtau.Core.SignalChain {
    public class WaveSource : ISignalSource {
        public readonly double offsetMs;
        public readonly double estimatedLengthMs;
        public readonly int offset;
        public readonly int estimatedLength;
        public readonly int channels;
        private readonly int sampleRate;

        public double EndMs => offsetMs + estimatedLengthMs;
        public bool HasSamples => data != null;
        public int SampleRate => sampleRate;
        public int Channels => AudioSettings.Channels; // 混音目标声道（交织布局）

        private readonly object lockObj = new object();
        // volatile：批 2 渲染线程锁内 SetSamples 与音频线程锁外 Mix/IsReady 并发
        //（C-3 两批策略热路径——非 volatile 可见性延迟会致残留静音）
        private volatile float[] data;

        public WaveSource(double offsetMs, double estimatedLengthMs, double skipOverMs, int channels,
                          int? sampleRate = null) {
            this.offsetMs = offsetMs;
            this.estimatedLengthMs = estimatedLengthMs;
            this.channels = channels;
            this.sampleRate = sampleRate ?? AudioSettings.SampleRate;
            offset = (int)((offsetMs - skipOverMs) * this.sampleRate / 1000) * channels;
            estimatedLength = (int)(estimatedLengthMs * this.sampleRate / 1000) * channels;
        }

        public void SetSamples(float[] samples) {
            lock (lockObj) {
                data = samples;
            }
        }

        public bool IsReady(int position, int count) {
            int copies = AudioSettings.Channels / channels;
            return position + count <= offset * copies
                || offset * copies + estimatedLength * copies <= position
                || data != null;
        }

        public int Mix(int position, float[] buffer, int index, int count) {
            int copies = AudioSettings.Channels / channels;
            if (data == null) {
                if (position + count <= offset * copies) {
                    return position + count;
                }
                return position;
            }
            int start = Math.Max(position, offset * copies);
            int end = Math.Min(position + count, offset * copies + data.Length * copies);
            for (int i = start; i < end; ++i) {
                buffer[index + i - position] += data[i / copies - offset];
            }
            return end;
        }
    }
}
