using System.Collections.Generic;
using System.Threading;
using OpenUtau.App.Views;
using Xunit;

namespace OpenUtau.Test.App {
    public class RenderWindowExportTest {
        [Fact]
        public void DrainExport_DrainsUntilEof_AndProgressIsMonotonic() {
            var chunks = new Queue<int>(new[] { 4096, 4096, 0 });
            var seen = new List<long>();
            RenderWindow.DrainExport(
                () => chunks.Dequeue(),
                total => seen.Add(total),
                CancellationToken.None);

            Assert.Equal(new long[] { 4096, 8192 }, seen);
            // monotonic non-decreasing
            for (int i = 1; i < seen.Count; i++)
                Assert.True(seen[i] > seen[i - 1]);
        }

        [Fact]
        public void DrainExport_RespectsCancellation_AndDoesNotLoopForever() {
            // Reader never returns 0 — without cancellation this would hang.
            // Pre-cancelled token must break immediately.
            int calls = 0;
            RenderWindow.DrainExport(
                () => { calls++; return 4096; },
                _ => { },
                new CancellationToken(canceled: true));

            // Cancellation checked before first progress, after first read.
            // Loop breaks on the first iteration — bounded, not infinite.
            Assert.True(calls <= 1, $"expected at most 1 read, got {calls}");
        }

        [Fact]
        public void DrainExport_ImmediateEof_NoProgress() {
            var seen = new List<long>();
            RenderWindow.DrainExport(
                () => 0,
                total => seen.Add(total),
                CancellationToken.None);
            Assert.Empty(seen);
        }

        [Fact]
        public void DrainExport_ProgressIsAccumulated_NotPerChunk() {
            // The original bug: totalRead was reassigned each iteration (not accumulated),
            // so progress reflected only the last chunk. Verify accumulation.
            var chunks = new Queue<int>(new[] { 100, 200, 300, 0 });
            long last = -1;
            RenderWindow.DrainExport(
                () => chunks.Dequeue(),
                total => last = total,
                CancellationToken.None);
            Assert.Equal(600, last); // 100+200+300, not 300
        }
    }
}
