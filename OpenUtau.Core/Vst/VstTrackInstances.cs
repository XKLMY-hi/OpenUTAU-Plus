using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Core.SignalChain;
using Serilog;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Per-track VST effect instances — load/unload on demand, read-only during rendering.
    /// UI thread calls LoadAt/UnloadAt; audio thread calls GetActiveEffects (lock-free read).
    /// </summary>
    public class VstTrackInstances : IDisposable {
        private readonly int _trackNo;
        private readonly object _writeLock = new();
        public IVstBridge Bridge { get; set; } = RealVstBridge.Instance;

        // Array of loaded effects (null = empty slot). Only modified under _writeLock.
        // Audio thread reads snapshot via GetActiveEffects which copies references.
        private volatile VstEffect?[] _effects = Array.Empty<VstEffect?>();

        // Effects unloaded via UnloadAt are moved here to avoid disposing while
        // the audio thread may still hold a snapshot reference.  Call FlushPendingDispose()
        // from the render cycle entry point after the previous cycle is guaranteed done.
        private readonly List<VstEffect?> _pendingDispose = new();

        // 串行化同 track 的并发原生加载（Load/Setup/RestoreState 秒级，锁外执行）
        private readonly SemaphoreSlim _loadGate = new(1, 1);

        public VstTrackInstances(int trackNo) => _trackNo = trackNo;

        /// <summary>
        /// Load or reload the effect at a given slot index.
        /// Disposes old instance, creates new one, restores saved state.
        /// Called from UI thread (TrackEffectRack).
        /// </summary>
        public VstEffect? LoadAt(int index, VstPluginSlot slot) {
            lock (_writeLock) {
                UnloadAtLocked(index);

                if (!slot.IsLoaded || slot.Bypassed) return null;

                try {
                    var fx = new VstEffect(slot, Bridge);
                    fx.Load();
                    fx.Setup(AudioSettings.SampleRate, 4096);

                    // Restore saved state if available
                    if (slot.StateData != null)
                        fx.RestoreState(slot.StateData);

                    EnsureCapacity(index + 1);
                    _effects[index] = fx;
                    Log.Information($"[VstTrack] T{_trackNo}S{index}: {fx.DisplayName}");
                    return fx;
                } catch (Exception ex) {
                    Log.Warning($"[VstTrack] T{_trackNo}S{index} load failed: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Async load: 原生调用（Load/Setup/RestoreState，秒级）在 _writeLock 外执行，
        /// _loadGate 串行化同 track 并发加载；仅数组替换在锁内。
        /// 加载期间音频线程继续使用旧实例（平滑过渡），完成后旧实例进延迟销毁队列。
        /// </summary>
        public async Task<VstEffect?> LoadAtAsync(int index, VstPluginSlot slot, CancellationToken ct = default) {
            await _loadGate.WaitAsync(ct).ConfigureAwait(false);
            try {
                if (ct.IsCancellationRequested) return null;
                if (!slot.IsLoaded || slot.Bypassed) {
                    UnloadAt(index);
                    return null;
                }

                var fx = new VstEffect(slot, Bridge);
                await Task.Run(() => {
                    fx.Load();
                    fx.Setup(AudioSettings.SampleRate, 4096);
                    if (slot.StateData != null)
                        fx.RestoreState(slot.StateData);
                }, ct).ConfigureAwait(false);

                lock (_writeLock) {
                    if (ct.IsCancellationRequested) {
                        fx.Dispose();
                        return null;
                    }
                    UnloadAtLocked(index);
                    EnsureCapacity(index + 1);
                    _effects[index] = fx;
                }
                Log.Information($"[VstTrack] T{_trackNo}S{index}: {fx.DisplayName} (async)");
                return fx;
            } catch (Exception ex) {
                Log.Warning($"[VstTrack] T{_trackNo}S{index} load failed: {ex.Message}");
                return null;
            } finally {
                _loadGate.Release();
            }
        }

        /// <summary>Unload the effect at a slot index.</summary>
        public void UnloadAt(int index) {
            lock (_writeLock) { UnloadAtLocked(index); }
        }

        private void UnloadAtLocked(int index) {
            if (index < 0 || index >= _effects.Length) return;
            var fx = _effects[index];
            if (fx != null) {
                // Save state before disposal so user params persist across reloads
                var state = fx.SaveState();
                if (state != null && fx.Slot != null) fx.Slot.StateData = state;

                // Defer Dispose — audio thread may still be processing this effect.
                _effects[index] = null;
                _pendingDispose.Add(fx);
            }
        }

        /// <summary>
        /// Safely dispose all effects queued for removal.  Must only be called when
        /// the audio render cycle is not using any snapshot of the old _effects array.
        /// </summary>
        public void FlushPendingDispose() {
            List<VstEffect?>? toDispose = null;
            lock (_writeLock) {
                if (_pendingDispose.Count == 0) return;
                toDispose = new List<VstEffect?>(_pendingDispose);
                _pendingDispose.Clear();
            }
            if (toDispose != null) {
                foreach (var fx in toDispose) fx?.Dispose();
            }
        }

        private void EnsureCapacity(int size) {
            if (_effects.Length >= size) return;
            var next = new VstEffect?[size];
            Array.Copy(_effects, next, _effects.Length);
            _effects = next;
        }

        /// <summary>
        /// Get non-null, non-bypassed effects snapshot. Called from audio thread.
        /// Volatile read ensures visibility of latest _effects reference.
        /// </summary>
        public IReadOnlyList<VstEffect> GetActiveEffects() {
            var src = _effects; // volatile read, snapshot
            var list = new List<VstEffect>(src.Length);
            for (int i = 0; i < src.Length; i++) {
                var fx = src[i];
                if (fx != null && !fx.IsBypassed)
                    list.Add(fx);
            }
            return list;
        }

        /// <summary>Get effect at slot index (null if empty).</summary>
        public VstEffect? this[int i] {
            get {
                var src = _effects;
                return i >= 0 && i < src.Length ? src[i] : null;
            }
        }

        public void Dispose() {
            FlushPendingDispose();
            lock (_writeLock) {
                for (int i = 0; i < _effects.Length; i++) {
                    _effects[i]?.Dispose();
                    _effects[i] = null;
                }
            }
        }
    }
}
