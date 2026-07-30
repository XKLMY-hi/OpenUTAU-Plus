using System;
using System.Text;
using K4os.Hash.xxHash;
using OpenUtau.Core.Vst;
using Xunit;

namespace OpenUtau.Test.Core.Vst {
    public class VstPluginRegistryTest {
        [Fact]
        public void BuildVst2Uid_DeterministicForSameName() {
            string a = VstPluginRegistry.BuildVst2Uid("Synth1");
            string b = VstPluginRegistry.BuildVst2Uid("Synth1");
            Assert.Equal(a, b);
        }

        [Fact]
        public void BuildVst2Uid_DifferentForDifferentNames() {
            string a = VstPluginRegistry.BuildVst2Uid("Synth1");
            string b = VstPluginRegistry.BuildVst2Uid("Synth2");
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void BuildVst2Uid_HasCorrectFormat() {
            string uid = VstPluginRegistry.BuildVst2Uid("Synth1");
            Assert.StartsWith("vst2:", uid);
            string hex = uid.Substring("vst2:".Length);
            Assert.Equal(16, hex.Length);
            // lowercase hex
            foreach (char c in hex)
                Assert.True((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
                    $"unexpected char {c} in uid {uid}");
        }

        [Fact]
        public void BuildVst2Uid_MatchesIndependentXxHash() {
            string dllName = "MyPlugin";
            string uid = VstPluginRegistry.BuildVst2Uid(dllName);
            ulong expected = XXH64.DigestOf(Encoding.UTF8.GetBytes(dllName));
            Assert.Equal($"vst2:{expected:x8}", uid);
        }
    }
}
