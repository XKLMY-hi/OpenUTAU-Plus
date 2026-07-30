using System;
using System.Collections.Generic;
#nullable enable
using NAudio.Wave;
using OpenUtau.Core.SignalChain.Effects;

namespace OpenUtau.Core.SignalChain {
    /// <summary>Hand-rolled test fakes — no mocking framework, matches WaveSourceTest style.</summary>
    internal static class Fakes {
        public static WaveFormat Stereo441 => WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    }

    /// <summary>IEffect stub that records Reset/Process calls.</summary>
    internal class FakeEffect : IEffect {
        public int ResetCount { get; private set; }
        public int ProcessCount { get; private set; }
        public bool IsBypassed { get; set; }
        public void Process(float[] buffer, int offset, int count) => ProcessCount++;
        public void Reset() => ResetCount++;
    }

    /// <summary>ISampleProvider that returns chunks from a preset queue.</summary>
    internal class FakeSampleSource : ISampleProvider {
        private readonly Queue<int> _chunks;
        public WaveFormat WaveFormat { get; }
        public int TotalReadCalls { get; private set; }

        public FakeSampleSource(WaveFormat? format = null, params int[] chunks) {
            WaveFormat = format ?? Fakes.Stereo441;
            _chunks = new Queue<int>(chunks);
        }
        public int Read(float[] buffer, int offset, int count) {
            TotalReadCalls++;
            if (_chunks.Count == 0) return 0;
            int n = _chunks.Dequeue();
            n = Math.Min(n, count);
            for (int i = 0; i < n; i++) buffer[offset + i] = 0f;
            return n;
        }
    }

    /// <summary>ISignalSource with controllable IsReady/Mix behaviour.</summary>
    internal class FakeSignalSource : ISignalSource {
        public bool Ready { get; set; } = true;
        public int MixReturn { get; set; }
        public int MixCalls { get; private set; }
        public bool IsReady(int position, int count) => Ready;
        public int Mix(int position, float[] buffer, int index, int count) {
            MixCalls++;
            return MixReturn > 0 ? MixReturn : position + count;
        }
    }
}
