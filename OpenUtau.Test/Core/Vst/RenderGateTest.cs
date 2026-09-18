using System;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Audio;
using Xunit;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// RenderGate 在飞计数 + TryFlushAllPendingDispose 的条件拒绝。
    /// 注意：VstPluginManager.Inst 是单例且依赖 PlaybackManager（DummyAudioOutput
    /// 默认 Stopped → OutputActive == false），测试间共享状态——每用例 ClearAll 兜底。
    /// 共享静态计数的测试方法必须串行（类内并行会互相干扰精确断言）。
    /// </summary>
    [Collection("VstShared")]
    public class RenderGateTest {
        [Fact]
        public void EnterLeave_CountsInFlight() {
            // 同集合内先跑的用例可能仍有在飞闸门（单例/异步残留）——断言相对基线，
            // 只验证本用例的进出精确配对，不对全局零值做假设。
            int baseline = RenderGate.InFlight;
            using var a = RenderGate.Enter();
            Assert.Equal(baseline + 1, RenderGate.InFlight);
            using var b = RenderGate.Enter();
            Assert.Equal(baseline + 2, RenderGate.InFlight);
            b.Dispose();
            Assert.Equal(baseline + 1, RenderGate.InFlight);
            a.Dispose();
            Assert.Equal(baseline, RenderGate.InFlight);
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
            // 下界取"进入前的全局基线 + 自己这一次"：同集合内其他用例（单例异步残留、
            // 并行集合的渲染段）可能让全局计数非零——不变式应对基线成立，而非对 0 成立。
            int baseline = RenderGate.InFlight;
            var tasks = new Task[N];
            for (int i = 0; i < N; i++) {
                tasks[i] = Task.Run(() => {
                    for (int k = 0; k < 100; k++) {
                        using var g = RenderGate.Enter();
                        Assert.True(RenderGate.InFlight > baseline);
                    }
                });
            }
            await Task.WhenAll(tasks);
            Assert.Equal(baseline, RenderGate.InFlight);
        }
    }
}
