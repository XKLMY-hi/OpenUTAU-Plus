using System;
using OpenUtau.Core.SignalChain.Effects;
using Serilog;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// A loaded VST plugin instance — the single source of truth.
    /// Owns the native bridge handle.  Shared between RenderEngine
    /// (audio processing) and VstEditorWindow (native GUI).
    /// </summary>
    public class VstEffect : IEffect, IDisposable {
        private readonly VstPluginSlot _slot;
        private readonly VstPluginEntry _entry;
        private IntPtr _handle;
        private bool _setupDone;
        public VstPluginSlot Slot => _slot;
        public VstPluginEntry Entry => _entry;
        public string DisplayName => _entry.Name;
        public bool IsLoaded => _handle != IntPtr.Zero;
        public IntPtr GetBridgeHandle() => _handle;

        public VstEffect(VstPluginSlot slot) {
            _slot = slot;
            _entry = VstPluginRegistry.Inst.TryGet(slot.PluginUid)
                     ?? throw new InvalidOperationException($"Plugin not found: {slot.PluginUid}");
        }

        // ── Lifecycle ──────────────────────────────────────────

        public void Load() {
            if (_handle != IntPtr.Zero) return;
            if (!_entry.IsEffect)
                throw new InvalidOperationException(
                    $"Cannot load instrument '{_entry.Name}' as an effect.");

            _handle = VstBridge.Load(_entry.Path);
            if (_handle == IntPtr.Zero) {
                string? err = VstBridge.LastError();
                throw new InvalidOperationException(
                    $"Failed to load '{_entry.Name}': {err ?? "unknown"}");
            }
            Log.Information($"[VstEffect] Loaded '{_entry.Name}' (0x{_handle:X})");
        }

        public void Setup(double sampleRate, int maxBlockSize) {
            if (_handle == IntPtr.Zero || _setupDone) return;
            if (!VstBridge.Setup(_handle, sampleRate, maxBlockSize)) {
                Log.Warning($"[VstEffect] Setup failed: {VstBridge.LastError()}");
                return;
            }
            if (!VstBridge.Activate(_handle, true)) {
                Log.Warning($"[VstEffect] Activate failed: {VstBridge.LastError()}");
                return;
            }
            _setupDone = true;
        }

        // ── IEffect ────────────────────────────────────────────

        public bool IsBypassed => _slot.Bypassed;
        public void Process(float[] buffer, int offset, int count) {
            if (_slot.Bypassed || _handle == IntPtr.Zero) return;
            int frames = count / 2;
            if (frames <= 0) return;

            if (offset == 0 && buffer.Length == count) {
                VstBridge.Process(_handle, buffer, frames);
            } else {
                float[] slice = new float[count];
                Array.Copy(buffer, offset, slice, 0, count);
                VstBridge.Process(_handle, slice, frames);
                Array.Copy(slice, 0, buffer, offset, count);
            }
        }
        public void Reset() {
            if (_handle != IntPtr.Zero) VstBridge.Reset(_handle);
        }

        // ── Native GUI ─────────────────────────────────────────

        /// <summary>
        /// Open native editor in a Win32 popup window.
        /// Each call creates a new window (multiple windows allowed).
        /// </summary>
        public bool OpenNativeEditor() {
            if (_handle == IntPtr.Zero) return false;
            bool ok = VstBridge.OpenEditorWindow(_handle);
            if (ok)
                Log.Information($"[VstEffect] Native editor opened for '{_entry.Name}'");
            else
                Log.Warning($"[VstEffect] Editor failed for '{_entry.Name}': {VstBridge.LastError()}");
            return ok;
        }

        // ── State Persistence ──────────────────────────────────

        /// <summary>Save processor state (for .ustxp). Returns null on failure.</summary>
        public byte[]? SaveState() {
            if (_handle == IntPtr.Zero) return null;
            return VstBridge.SaveState(_handle);
        }

        /// <summary>Restore processor state (from .ustxp). Returns true on success.</summary>
        public bool RestoreState(byte[]? data) {
            if (_handle == IntPtr.Zero || data == null || data.Length == 0) return false;
            return VstBridge.RestoreState(_handle, data);
        }

        // ── IDisposable ────────────────────────────────────────

        public void Dispose() {
            if (_handle != IntPtr.Zero) {
                if (_setupDone) {
                    VstBridge.Activate(_handle, false);
                    _setupDone = false;
                }
                VstBridge.Unload(_handle);
                Log.Information($"[VstEffect] Disposed '{_entry.Name}' (0x{_handle:X})");
                _handle = IntPtr.Zero;
            }
        }
    }
}
