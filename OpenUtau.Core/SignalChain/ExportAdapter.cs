using System;
using NAudio.Wave;

namespace OpenUtau.Core.SignalChain {
    public class ExportAdapter : ISampleProvider {
        private readonly WaveFormat waveFormat;
        private readonly ISignalSource source;
        private int position;

        public WaveFormat WaveFormat => waveFormat;

        public ExportAdapter(ISignalSource source) {
            this.source = source;
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.SampleRate, source.Channels);
        }

        public int Read(float[] buffer, int offset, int count) {
            for (int i = offset; i < offset + count; ++i) {
                buffer[i] = 0;
            }
            if (!source.IsReady(position, count)) {
                throw new Exception("All sources must be ready when exporting.");
            } else {
                int pos = source.Mix(position, buffer, offset, count);
                int n = Math.Max(0, pos - position);
                position = pos;
                return n;
            }
        }
    }
}
