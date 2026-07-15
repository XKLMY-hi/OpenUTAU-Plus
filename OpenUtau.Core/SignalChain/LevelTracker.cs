using System;

namespace OpenUtau.Core.SignalChain {
    public class LevelTracker : ISignalSource {
        private readonly ISignalSource inner;
        public volatile float Peak;
        private int _callCount;

        public LevelTracker(ISignalSource inner) { this.inner = inner; }

        public bool IsReady(int position, int count) => inner.IsReady(position, count);

        public int Mix(int position, float[] buffer, int index, int count) {
            int ret = inner.Mix(position, buffer, index, count);
            float max = 0;
            for (int i = 0; i < count; i++) {
                float abs = MathF.Abs(buffer[index + i]);
                if (abs > max) max = abs;
            }
            if (max > Peak) Peak = max;
            // Log every ~100 calls to confirm data flow
            if (++_callCount % 100 == 0 && Peak > 0.0001f) {
                Serilog.Log.Debug($"[LevelTracker] peak={Peak:F4} calls={_callCount}");
            }
            return ret;
        }

        public float ReadAndResetPeakDb() {
            float p = Peak; Peak = 0;
            if (p <= 0.00001f) return -60f;
            return Math.Clamp(20f * MathF.Log10(p), -60f, 0f);
        }
    }
}

