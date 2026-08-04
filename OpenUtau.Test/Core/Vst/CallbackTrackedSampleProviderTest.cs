using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Xunit;

namespace OpenUtau.Audio {
    /// <summary>
    /// CallbackTrackedSampleProvider 的在飞标志语义（B1 drain 屏障的载体）。
    /// 轮询断言在重负载并行下会超时——纳入 VstShared 串行集合。
    /// </summary>
    [Xunit.Collection("VstShared")]
    public class CallbackTrackedSampleProviderTest {
        // 提供已知样本的 provider：Read 期间并发检查 InCallback
        sealed class ProbeProvider : ISampleProvider {
            public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            private readonly float[] data;
            private int pos;
            private readonly int readDelayMs;

            public ProbeProvider(int samples, int readDelayMs = 0) {
                data = Enumerable.Range(0, samples).Select(i => (float)i).ToArray();
                this.readDelayMs = readDelayMs;
            }

            public int Read(float[] buffer, int offset, int count) {
                // 人为拉长 Read——让并发轮询有机会观察到 InCallback == true
                if (readDelayMs > 0) Thread.Sleep(readDelayMs);
                int n = Math.Min(count, data.Length - pos);
                if (n > 0) Array.Copy(data, pos, buffer, offset, n);
                pos += n;
                return n;
            }
        }

        [Fact]
        public void InCallback_FalseWhenIdle() {
            var inner = new ProbeProvider(64);
            var wrapped = new CallbackTrackedSampleProvider(inner);
            Assert.False(wrapped.InCallback);
            Assert.Equal(inner.WaveFormat, wrapped.WaveFormat);
        }

        [Fact]
        public async Task InCallback_TrueDuringRead_FromAnotherThread() {
            var inner = new ProbeProvider(4096, readDelayMs: 100);
            var wrapped = new CallbackTrackedSampleProvider(inner);
            var buf = new float[4096];

            // Read 在后台线程执行（100ms）；前台并发轮询 InCallback 直到观察到 true
            var readTask = Task.Run(() => wrapped.Read(buf, 0, buf.Length));
            bool observed = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!readTask.IsCompleted) {
                if (wrapped.InCallback) { observed = true; break; }
                Thread.Yield();
                Assert.True(sw.ElapsedMilliseconds < 3000, "Read 未完成前应观察到 InCallback == true");
            }
            await readTask;
            Assert.True(observed, "后台 Read 期间 InCallback 应为 true");
            Assert.False(wrapped.InCallback);
            Assert.Equal(4096, readTask.Result);
        }

        [Fact]
        public void Read_ChainedCalls_FlagCleared() {
            var wrapped = new CallbackTrackedSampleProvider(new ProbeProvider(128));
            var buf = new float[128];
            wrapped.Read(buf, 0, 64);
            Assert.False(wrapped.InCallback);
            wrapped.Read(buf, 64, 64);
            Assert.False(wrapped.InCallback);
        }
    }
}
