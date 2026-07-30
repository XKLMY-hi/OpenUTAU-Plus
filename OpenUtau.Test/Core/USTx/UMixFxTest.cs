using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Core.Ustx {
    public class UMixFxTest {
        [Fact]
        public void Default_IsNotEnabled() {
            var fx = new UMixFx();
            Assert.False(fx.Enabled);
        }

        [Fact]
        public void YamlRoundTrip_PreservesEqParams() {
            var fx = new UMixFx {
                Enabled = true,
                EqLowDb = 3.5, EqMidFreq = 1000.0, EqMidDb = -2.0, EqHighDb = 1.0,
                EqBypassed = false,
            };
            var yaml = Yaml.DefaultSerializer.Serialize(fx);
            var restored = Yaml.DefaultDeserializer.Deserialize<UMixFx>(yaml);
            Assert.NotNull(restored);
            Assert.True(restored.Enabled);
            Assert.Equal(3.5, restored.EqLowDb);
            Assert.Equal(1000.0, restored.EqMidFreq);
            Assert.Equal(-2.0, restored.EqMidDb);
            Assert.Equal(1.0, restored.EqHighDb);
            Assert.False(restored.EqBypassed);
        }

        [Fact]
        public void YamlRoundTrip_PreservesCompAndRevParams() {
            var fx = new UMixFx {
                Enabled = true,
                CompThresholdDb = -18.0, CompRatio = 4.0,
                CompPreset = "Soft", ReverbPreset = "Hall",
                ReverbWet = 0.5, ReverbSize = 0.8,
            };
            var yaml = Yaml.DefaultSerializer.Serialize(fx);
            var restored = Yaml.DefaultDeserializer.Deserialize<UMixFx>(yaml);
            Assert.Equal(-18.0, restored.CompThresholdDb);
            Assert.Equal(4.0, restored.CompRatio);
            Assert.Equal("Soft", restored.CompPreset);
            Assert.Equal("Hall", restored.ReverbPreset);
            Assert.Equal(0.5, restored.ReverbWet);
            Assert.Equal(0.8, restored.ReverbSize);
        }

        [Fact]
        public void Disabled_NullEquivalent() {
            var disabled = new UMixFx { Enabled = false };
            var yaml = Yaml.DefaultSerializer.Serialize(disabled);
            var restored = Yaml.DefaultDeserializer.Deserialize<UMixFx>(yaml);
            Assert.False(restored.Enabled);
        }
    }
}
