using OpenUtau.Core.SignalChain;
using OpenUtau.Core.SignalChain.Effects;
using Xunit;

namespace OpenUtau.Test.Core.SignalChain {
    public class EffectChainTest {
        [Fact]
        public void Reset_CallsResetOnAllEffects() {
            var fx1 = new FakeEffect();
            var fx2 = new FakeEffect { IsBypassed = true };
            var chain = new EffectChain(new FakeSignalSource(), new IEffect[] { fx1, fx2 });

            chain.Reset();

            Assert.Equal(1, fx1.ResetCount);
            Assert.Equal(1, fx2.ResetCount); // bypassed effects are still reset
        }

        [Fact]
        public void Reset_OnEmptyChain_DoesNotThrow() {
            var chain = new EffectChain(new FakeSignalSource(), new IEffect[0]);
            var ex = Record.Exception(() => chain.Reset());
            Assert.Null(ex);
        }

        [Fact]
        public void MasterAdapter_SetPosition_TriggersEffectChainReset() {
            var fx = new FakeEffect();
            var chain = new EffectChain(new FakeSignalSource(), new IEffect[] { fx });
            var master = new MasterAdapter(chain);

            master.SetPosition(1000);

            Assert.Equal(1, fx.ResetCount);
        }

        [Fact]
        public void MasterAdapter_SetPosition_NonEffectChainSource_DoesNotThrow() {
            // WaveMix (empty) is NOT an EffectChain — SetPosition must not throw
            var master = new MasterAdapter(new WaveMix(new ISignalSource[0]));
            var ex = Record.Exception(() => master.SetPosition(500));
            Assert.Null(ex);
        }
    }
}
