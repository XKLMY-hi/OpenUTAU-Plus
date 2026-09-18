using System;
using System.Threading;

namespace OpenUtau.Audio {
    /// <summary>
    /// 渲染/导出"消费段"在飞计数。由**消费方**（播放、录制导出、分轨导出）进入，
    /// 覆盖所有"持有信号链快照并消费 Mix"的代码段。
    ///
    /// 语义定位（合成/渲染解耦后）：本类属于音频消费层，不属于 VST——VST 延迟销毁
    /// 只是它的一个消费者（VstPluginManager.TryFlushAllPendingDispose 只在
    /// InFlight == 0 时才允许释放队列中的旧 handle）。
    ///
    /// 合成层（Render/RenderEngine）与播放层（PlaybackManager）都引用它，但都不拥有它：
    /// 任何新的合成后端只要在"消费已渲染音频"期间 Enter 即可复用同一保护。
    /// </summary>
    public static class RenderGate {
        private static int inFlight;

        /// <summary>当前在飞的渲染/导出消费段数量。</summary>
        public static int InFlight => Volatile.Read(ref inFlight);

        /// <summary>进入一个消费段；返回的 IDisposable 离开时释放（配 using 语句）。</summary>
        public static IDisposable Enter() {
            Interlocked.Increment(ref inFlight);
            return new Leaver();
        }

        private sealed class Leaver : IDisposable {
            public void Dispose() => Interlocked.Decrement(ref inFlight);
        }
    }
}
