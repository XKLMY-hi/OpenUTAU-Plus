using System;
using System.Threading;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// 渲染期在飞计数（RenderGate）。覆盖所有"持有信号链快照并消费 Mix"的代码段
    /// （导出写文件循环、渲染任务）。VST 延迟销毁的 Flush 只在 InFlight == 0 时执行——
    /// 加上输出未播放（PlaybackManager.OutputActive == false），才可安全 Dispose 延迟
    /// 队列中的旧 handle（B1 竞态修复的第二道屏障，第一道是 AudioOutput 的 drain）。
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
