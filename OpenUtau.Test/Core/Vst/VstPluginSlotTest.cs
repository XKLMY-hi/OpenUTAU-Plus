using System;
using OpenUtau.Core.Vst;
using Xunit;
#nullable enable

namespace OpenUtau.Test.Core.Vst {
    public class VstPluginSlotTest {
        [Fact]
        public void ValidBase64_RoundTrips() {
            var slot = new VstPluginSlot();
            byte[] data = { 1, 2, 3, 4, 5 };
            slot.StateDataBase64 = Convert.ToBase64String(data);
            Assert.Equal(data, slot.StateData);
            Assert.Equal(Convert.ToBase64String(data), slot.StateDataBase64);
        }

        [Fact]
        public void CorruptBase64_DoesNotThrow_AndNullsState() {
            var slot = new VstPluginSlot();
            slot.StateData = new byte[] { 9, 9, 9 };
            Assert.NotNull(slot.StateData);

            // Corrupt base64 must not throw; state degrades to null
            slot.StateDataBase64 = "!!!not-base64!!!";
            Assert.Null(slot.StateData);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void EmptyOrNull_Base64_YieldsNullState(string? value) {
            var slot = new VstPluginSlot { StateDataBase64 = value };
            Assert.Null(slot.StateData);
        }

        [Fact]
        public void CorruptAfterValid_DoesNotRetainStaleState() {
            var slot = new VstPluginSlot();
            slot.StateDataBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
            Assert.NotNull(slot.StateData);

            slot.StateDataBase64 = "@@@invalid@@@";
            Assert.Null(slot.StateData);
        }
    }
}
