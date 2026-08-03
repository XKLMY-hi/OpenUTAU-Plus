namespace OpenUtau.Core.SignalChain {
    public interface ISignalSource {
        /// <summary>采样率。默认取全局 AudioSettings；已知格式的实现覆写。</summary>
        int SampleRate => AudioSettings.SampleRate;
        /// <summary>声道数（交织布局）。默认取全局 AudioSettings。</summary>
        int Channels => AudioSettings.Channels;

        bool IsReady(int position, int count);
        /// <summary>
        /// Add float audio samples to existing buffer values.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns>End position after read.</returns>
        int Mix(int position, float[] buffer, int index, int count);

        /// <summary>
        /// Drop time-domain state.  Default no-op.  EffectChain overrides to flush
        /// reverb tails, compressor envelopes, etc. on seek.
        /// </summary>
        void Reset() { } // C# 8 default interface method
    }
}
