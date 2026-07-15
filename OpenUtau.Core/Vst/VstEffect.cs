using System;
using OpenUtau.Core.SignalChain.Effects;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Wraps a VST plugin instance as an IEffect for insertion into the EffectChain.
    ///
    /// Audio path (Phase 3 — C++ bridge integration):
    ///   Process() → vst_process(_handle, buffer, offset, count/2)
    ///
    /// Currently passes audio through unchanged (bridge stub).
    /// </summary>
    public class VstEffect : IEffect, IDisposable {
        private readonly VstPluginSlot _slot;
        private readonly VstPluginEntry _entry;
        private IntPtr _handle;

        public VstPluginSlot Slot => _slot;
        public string DisplayName => _entry.Name;

        public VstEffect(VstPluginSlot slot) {
            _slot = slot;
            _entry = VstPluginRegistry.Inst.TryGet(slot.PluginUid)
                     ?? throw new InvalidOperationException($"Plugin not found: {slot.PluginUid}");
        }

        /// <summary>
        /// Load the native plugin. Currently a stub — passes audio through.
        /// Phase 3: calls vst_load() from the C++ bridge.
        /// </summary>
        public void Load() {
            if (!_entry.IsEffect)
                throw new InvalidOperationException(
                    $"Cannot load instrument plugin '{_entry.Name}' as an effect. " +
                    "Only audio effect VSTs can be inserted in the effect chain.");
            // Stub: _handle = VstBridge.Load(_entry.Path);
            _handle = IntPtr.Zero;
        }

        // ── IEffect ─────────────────────────────────────────────

        public bool IsBypassed => _slot.Bypassed;

        public void Process(float[] buffer, int offset, int count) {
            if (_slot.Bypassed) return;
            // Phase 3 stub: VstBridge.Process(_handle, ref buffer[offset], count / 2);
        }

        public void Reset() {
            // Phase 3 stub: VstBridge.Reset(_handle);
        }

        public void Dispose() {
            if (_handle != IntPtr.Zero) {
                // Phase 3 stub: VstBridge.Unload(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
