using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// RenderGate 在飞计数 + TryFlushAllPendingDispose 的条件拒绝。
    /// 注意：VstPluginManager.Inst 是单例且依赖 PlaybackManager（DummyAudioOutput
    /// 默认 Stopped → OutputActive == false），测试间共享状态——每用例 ClearAll 兜底。
    /// </summary>
    public class RenderGateTest {
        [Fact]
        public void EnterLeave_CountsInFlight() {
            Assert.Equal(0, RenderGate.InFlight);
            using var a = RenderGate.Enter();
            Assert.Equal(1, RenderGate.InFlight);
            using var b = RenderGate.Enter();
            Assert.Equal(2, RenderGate.InFlight);
            b.Dispose();
            Assert.Equal(1, RenderGate.InFlight);
            a.Dispose();
            Assert.Equal(0, RenderGate.InFlight);
        }

        [Fact]
        public void TryFlush_RejectedWhileInFlight() {
            try {
                using var gate = RenderGate.Enter();
                Assert.False(VstPluginManager.Inst.TryFlushAllPendingDispose());
            } finally {
                VstPluginManager.Inst.ClearAll();
            }
        }

        [Fact]
        public void TryFlush_AllowedWhenIdle() {
            try {
                Assert.True(VstPluginManager.Inst.TryFlushAllPendingDispose());
            } finally {
                VstPluginManager.Inst.ClearAll();
            }
        }

        [Fact]
        public async Task EnterLeave_Concurrent_CountsStable() {
            const int N = 32;
            var tasks = new Task[N];
            for (int i = 0; i < N; i++) {
                tasks[i] = Task.Run(() => {
                    for (int k = 0; k < 100; k++) {
                        using var g = RenderGate.Enter();
                        Assert.True(RenderGate.InFlight >= 1);
                    }
                });
            }
            await Task.WhenAll(tasks);
            Assert.Equal(0, RenderGate.InFlight);
        }
    }
}
