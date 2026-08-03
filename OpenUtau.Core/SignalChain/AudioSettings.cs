using NAudio.Wave;

namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// 全局音频格式事实来源（此前为常量且零引用——硬编码散落各处）。
    /// 启动引导调 Configure 一次（当前固定 44100/2/4096，行为零变化；
    /// 未来采样率可配置时在此接入 Preferences）。
    /// 信号链各层通过 ISignalSource 的默认接口成员获得格式。
    /// </summary>
    public static class AudioSettings {
        public static int SampleRate { get; private set; } = 44100;
        public static int Channels { get; private set; } = 2;
        /// <summary>音频处理块大小（VST Setup 等）。</summary>
        public static int BlockSize { get; private set; } = 4096;

        /// <summary>启动时调用一次。</summary>
        public static void Configure(int sampleRate, int channels, int blockSize = 4096) {
            SampleRate = sampleRate;
            Channels = channels;
            BlockSize = blockSize;
        }

        public static WaveFormat CreateIeeeFloatWaveFormat()
            => WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
    }
}
