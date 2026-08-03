using System;
using System.Collections.Generic;

namespace OpenUtau.Core.Render {
    /// <summary>
    /// 内存短语渲染缓存（LRU）。key = RenderPhrase.hash（含 singerId/renderer/
    /// wavtool/timeAxis.Timestamp/phoneme hashes/后效曲线——编辑音符自动改 hash，
    /// 自动失效，正确性免费）。
    /// 与磁盘 phrase 缓存（res-*/wdl-*/cat-*.wav）是两层：hash 相同则磁盘必命中，
    /// 两者天然一致。容量按样本数上限封顶（64M ≈ 256MB 单声道 float），
    /// LoadProject 时 Clear。
    /// </summary>
    public sealed class PhraseRenderCache {
        private const long MaxTotalSamples = 64 * 1024 * 1024;

        private readonly LinkedList<(ulong hash, float[] samples)> _lru = new();
        private readonly Dictionary<ulong, LinkedListNode<(ulong hash, float[] samples)>> _index = new();
        private long _totalSamples;
        private readonly object _lock = new();

        public int Count {
            get { lock (_lock) return _index.Count; }
        }

        /// <summary>命中返回样本（内部引用，调用方不得修改）；未命中返回 null。</summary>
        public float[]? TryGet(ulong hash) {
            lock (_lock) {
                if (_index.TryGetValue(hash, out var node)) {
                    _lru.Remove(node);
                    _lru.AddFirst(node);
                    return node.Value.samples;
                }
                return null;
            }
        }

        public void Put(ulong hash, float[] samples) {
            if (samples == null || samples.Length == 0) return;
            lock (_lock) {
                if (_index.ContainsKey(hash)) return;
                var node = _lru.AddFirst((hash, samples));
                _index[hash] = node;
                _totalSamples += samples.Length;
                while (_totalSamples > MaxTotalSamples && _lru.Count > 1) {
                    var last = _lru.Last!;
                    _lru.RemoveLast();
                    _index.Remove(last.Value.hash);
                    _totalSamples -= last.Value.samples.Length;
                }
            }
        }

        public void Clear() {
            lock (_lock) {
                _lru.Clear();
                _index.Clear();
                _totalSamples = 0;
            }
        }
    }
}
