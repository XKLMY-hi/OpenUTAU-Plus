using System;
using Xunit;

namespace OpenUtau.Core.SignalChain {
    /// <summary>SpectrumBus SPSC 块环形缓冲：发布时序/覆盖/Reset/门控。</summary>
    public class SpectrumBusTest {
        [Fact]
        public void AddSamples_BlockFull_PublishesWithDownmix() {
            var bus = SpectrumBus.Inst;
            bus.Reset();
            bus.Enabled = true;
            try {
                // 满块：2048 mono 帧 = 4096 交织样本（2ch）
                float[] buf = new float[SpectrumBus.FftSize * 2];
                for (int i = 0; i < buf.Length; i++) buf[i] = (i % 2 == 0) ? 2f : -2f; // L=2, R=-2 → mono=0
                bus.AddSamples(buf, 0, buf.Length, 2);
                Assert.True(bus.TryGetLatestBlock(out var block, out long seq));
                Assert.Equal(1, seq);
                for (int i = 0; i < SpectrumBus.FftSize; i++) Assert.Equal(0f, block[i]); // (L+R)/2

                // 第二块：L=1,R=1 → mono=1
                for (int i = 0; i < buf.Length; i++) buf[i] = 1f;
                bus.AddSamples(buf, 0, buf.Length, 2);
                Assert.True(bus.TryGetLatestBlock(out block, out seq));
                Assert.Equal(2, seq);
                for (int i = 0; i < SpectrumBus.FftSize; i++) Assert.Equal(1f, block[i]);
            } finally {
                bus.Enabled = false;
                bus.Reset();
            }
        }

        [Fact]
        public void AddSamples_PartialBlock_NotPublished() {
            var bus = SpectrumBus.Inst;
            bus.Reset();
            bus.Enabled = true;
            try {
                // 2048 交织样本 = 1024 mono 帧（半块）；两次补满 2048 帧 → 发布
                float[] buf = new float[2048];
                bus.AddSamples(buf, 0, buf.Length, 2);
                Assert.False(bus.TryGetLatestBlock(out _, out _));

                bus.AddSamples(buf, 0, buf.Length, 2);
                Assert.True(bus.TryGetLatestBlock(out _, out long seq));
                Assert.Equal(1, seq);
            } finally {
                bus.Enabled = false;
                bus.Reset();
            }
        }

        [Fact]
        public void AddSamples_MonoChannel_CopiesDirectly() {
            var bus = SpectrumBus.Inst;
            bus.Reset();
            bus.Enabled = true;
            try {
                float[] buf = new float[SpectrumBus.FftSize];
                for (int i = 0; i < buf.Length; i++) buf[i] = i * 0.001f;
                bus.AddSamples(buf, 0, buf.Length, 1);
                Assert.True(bus.TryGetLatestBlock(out var block, out _));
                for (int i = 0; i < SpectrumBus.FftSize; i++) Assert.Equal(i * 0.001f, block[i], 5);
            } finally {
                bus.Enabled = false;
                bus.Reset();
            }
        }

        [Fact]
        public void AddSamples_Overflow_KeepsLatestBlock() {
            var bus = SpectrumBus.Inst;
            bus.Reset();
            bus.Enabled = true;
            try {
                float[] buf = new float[SpectrumBus.FftSize * 2];
                // 写 50 块（> RingCount 48），每块 mono 值 = 块号
                for (int b = 0; b < 50; b++) {
                    for (int i = 0; i < buf.Length; i++) buf[i] = b;
                    bus.AddSamples(buf, 0, buf.Length, 2);
                }
                Assert.True(bus.TryGetLatestBlock(out var block, out long seq));
                Assert.Equal(50, seq);
                for (int i = 0; i < SpectrumBus.FftSize; i++) Assert.Equal(49f, block[i]); // 最新块
            } finally {
                bus.Enabled = false;
                bus.Reset();
            }
        }

        [Fact]
        public void Reset_ClearsPublishSeq() {
            var bus = SpectrumBus.Inst;
            bus.Reset();
            bus.Enabled = true;
            try {
                float[] buf = new float[SpectrumBus.FftSize * 2];
                bus.AddSamples(buf, 0, buf.Length, 2);
                Assert.True(bus.TryGetLatestBlock(out _, out _));
                bus.Reset();
                Assert.False(bus.TryGetLatestBlock(out _, out _));
                // Reset 后可继续写入（新周期从 1 开始）
                bus.AddSamples(buf, 0, buf.Length, 2);
                Assert.True(bus.TryGetLatestBlock(out _, out long seq));
                Assert.Equal(1, seq);
            } finally {
                bus.Enabled = false;
                bus.Reset();
            }
        }

        [Fact]
        public void AddSamples_Disabled_NoPublish() {
            var bus = SpectrumBus.Inst;
            bus.Reset();
            bus.Enabled = false;
            try {
                float[] buf = new float[SpectrumBus.FftSize * 2];
                bus.AddSamples(buf, 0, buf.Length, 2);
                Assert.False(bus.TryGetLatestBlock(out _, out _));
            } finally {
                bus.Reset();
            }
        }
    }
}
