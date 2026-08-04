using System;
using System.Threading;

namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// 频谱数据总线：SPSC 覆盖式块环形缓冲——音频回调线程（MasterAdapter.Read，
    /// 主推子后）写入，UI 线程（SpectrumCanvas 的 DispatcherTimer）读取。
    ///
    /// 块 = FFT 窗口 = <see cref="FftSize"/> 个 mono 帧。生产者写完整个块才
    /// 发布序号（release），消费者拿到序号后零拷贝直接引用该块——块在被回绕
    /// 复用前（≈2.2s）消费者早已完成 FFT，读写永不竞争同一槽。
    ///
    /// 音频线程纪律：Enabled 早退 + 纯算术，零锁零分配零日志。
    /// Reset 必须在音频回调 drain 后调用（PlaybackManager.WaitForCallbackDrain），
    /// 事件顺序即锁。
    /// </summary>
    public sealed class SpectrumBus {
        public static readonly SpectrumBus Inst = new();

        public const int FftSize = 2048;   // 每块 mono 帧数（= FFT 窗口，bin 宽 ≈21.5Hz@44.1k）
        public const int RingCount = 48;   // 覆盖时长 ≈ 48*2048/44100 ≈ 2.2s

        /// <summary>门控：无 SpectrumCanvas 附着时音频线程直接返回，零开销。UI 线程写，音频线程 volatile 读。</summary>
        public bool Enabled {
            get => Volatile.Read(ref _enabled);
            set => Volatile.Write(ref _enabled, value);
        }

        private bool _enabled;
        private readonly float[][] _blocks;   // 预分配 [RingCount][FftSize]
        private int _fillPos;                 // 仅生产者访问
        private long _headSeq;                // 已发布块序号（volatile 语义）

        public SpectrumBus() {
            _blocks = new float[RingCount][];
            for (int i = 0; i < RingCount; i++) {
                _blocks[i] = new float[FftSize];
            }
        }

        /// <summary>
        /// 音频回调线程：把交织样本下混为 mono 写入"下一块"槽，块满后发布序号。
        /// 槽位：写 <c>_headSeq % RingCount</c>（_headSeq 指向最新已发布块，下一块
        /// 写它后面的槽）；消费者读 <c>(seq-1) % RingCount</c>——语义相反，勿改错。
        /// </summary>
        public void AddSamples(float[] buffer, int offset, int count, int channels) {
            if (!Volatile.Read(ref _enabled)) {
                return;
            }
            long seq = Volatile.Read(ref _headSeq);
            float[] cur = _blocks[(int)(seq % RingCount)];
            if (channels >= 2) {
                int frames = count / 2;
                int p = _fillPos;
                int i = offset;
                while (p < FftSize && frames > 0) {
                    cur[p++] = (buffer[i] + buffer[i + 1]) * 0.5f; // (L+R)/2 下混
                    i += 2;
                    frames--;
                }
                _fillPos = p;
            } else {
                int p = _fillPos;
                int i = offset;
                while (p < FftSize && i < offset + count) {
                    cur[p++] = buffer[i++];
                }
                _fillPos = p;
            }
            if (_fillPos == FftSize) {
                _fillPos = 0;
                Volatile.Write(ref _headSeq, seq + 1); // release：数据先于序号可见
            }
        }

        /// <summary>
        /// UI 线程：取最新已发布块（零拷贝引用）。无新块/已 Reset 时返回 false。
        /// </summary>
        public bool TryGetLatestBlock(out float[]? block, out long seq) {
            seq = Volatile.Read(ref _headSeq);
            if (seq <= 0) {
                block = null;
                return false;
            }
            block = _blocks[(int)((seq - 1) % RingCount)];
            return true;
        }

        /// <summary>
        /// 清空发布序号（播放停止/暂停/seek 时）。必须在音频回调 drain 后调用——
        /// 生产者可能正在写 _fillPos/槽，事件顺序（PlaybackManager 的
        /// WaitForCallbackDrain）保证此刻无在飞写入。
        /// </summary>
        public void Reset() {
            _fillPos = 0;
            Volatile.Write(ref _headSeq, 0);
        }
    }
}
