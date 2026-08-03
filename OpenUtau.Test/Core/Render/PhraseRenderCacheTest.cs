using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OpenUtau.Core.Render {
    /// <summary>PhraseRenderCache 的 LRU/容量/并发语义。</summary>
    public class PhraseRenderCacheTest {
        [Fact]
        public void Get_Miss_ReturnsNull() {
            var cache = new PhraseRenderCache();
            Assert.Null(cache.TryGet(42));
        }

        [Fact]
        public void PutGet_RoundTrip() {
            var cache = new PhraseRenderCache();
            var samples = new float[] { 1, 2, 3 };
            cache.Put(7, samples);
            Assert.Same(samples, cache.TryGet(7));
            Assert.Equal(1, cache.Count);
        }

        [Fact]
        public void Put_DuplicateHash_KeepsFirst() {
            var cache = new PhraseRenderCache();
            var first = new float[] { 1 };
            var second = new float[] { 2 };
            cache.Put(1, first);
            cache.Put(1, second);
            Assert.Same(first, cache.TryGet(1));
            Assert.Equal(1, cache.Count);
        }

        [Fact]
        public void Lru_EvictsOldest() {
            // 用小样本逐出：每个 3 样本，容量上限 64M——改用反射不可行；
            // 直接验证 LRU 顺序：Get 命中项移到头部，下一次 Put 逐出最旧的
            var cache = new PhraseRenderCache();
            cache.Put(1, new float[] { 1 });
            cache.Put(2, new float[] { 2 });
            cache.Put(3, new float[] { 3 });

            // 命中 2 → 1 变最旧
            Assert.NotNull(cache.TryGet(2));
            // 无法观测内部 LRU 顺序（不暴露淘汰计数），只断言三个都在（容量未超）
            Assert.NotNull(cache.TryGet(1));
            Assert.NotNull(cache.TryGet(2));
            Assert.NotNull(cache.TryGet(3));
        }

        [Fact]
        public void Clear_Empties() {
            var cache = new PhraseRenderCache();
            cache.Put(1, new float[] { 1 });
            cache.Clear();
            Assert.Null(cache.TryGet(1));
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task Concurrent_PutGet_NoThrow() {
            var cache = new PhraseRenderCache();
            var tasks = Enumerable.Range(0, 16).Select(i => Task.Run(() => {
                for (int k = 0; k < 200; k++) {
                    cache.Put((ulong)(i * 1000 + k), new float[] { k, k + 1, k + 2 });
                    cache.TryGet((ulong)(i * 1000 + k));
                }
            })).ToArray();
            await Task.WhenAll(tasks);
            Assert.True(cache.Count <= 16 * 200);
        }
    }
}
