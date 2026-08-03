using System.Threading;
using NAudio.Wave;

namespace OpenUtau.Audio {
    /// <summary>
    /// Wraps an ISampleProvider and tracks whether the audio callback thread
    /// is currently inside a Read call.  Used as a drain barrier (B1 race fix):
    /// after AudioOutput.Stop(), wait until InCallback == false to know the
    /// audio thread can no longer touch the previous signal chain / VST handles.
    /// Both output implementations (NAudio / MiniAudio) reach the provider only
    /// through Read, so this wrapper is effective without touching them.
    /// </summary>
    public sealed class CallbackTrackedSampleProvider : ISampleProvider {
        private readonly ISampleProvider inner;
        private int inCallback; // 0/1, written with Interlocked.Exchange

        public CallbackTrackedSampleProvider(ISampleProvider inner) {
            this.inner = inner;
        }

        public WaveFormat WaveFormat => inner.WaveFormat;

        /// <summary>True while the audio callback thread is inside Read.</summary>
        public bool InCallback => Volatile.Read(ref inCallback) != 0;

        public int Read(float[] buffer, int offset, int count) {
            Interlocked.Exchange(ref inCallback, 1);
            try {
                return inner.Read(buffer, offset, count);
            } finally {
                Interlocked.Exchange(ref inCallback, 0);
            }
        }
    }
}
