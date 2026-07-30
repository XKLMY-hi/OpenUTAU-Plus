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
        private readonly IVstBridge _bridge;
        private IntPtr _handle;
        private bool _isSetup;
        private bool _isActivated;
        public VstPluginSlot Slot => _slot;
        public VstPluginEntry Entry => _entry;
        public string DisplayName => _entry.Name;
        public bool IsLoaded => _handle != IntPtr.Zero;
        public IntPtr GetBridgeHandle() => _handle;

        public VstEffect(VstPluginSlot slot, IVstBridge? bridge = null) {
            _slot = slot;
            _bridge = bridge ?? RealVstBridge.Instance;
            _entry = VstPluginRegistry.Inst.TryGet(slot.PluginUid)
                     ?? throw new InvalidOperationException($"Plugin not found: {slot.PluginUid}");
        }

        // ── Lifecycle ──────────────────────────────────────────

        public void Load() {
            if (_handle != IntPtr.Zero) return;
            if (!_entry.IsEffect)
                throw new InvalidOperationException(
                    $"Cannot load instrument '{_entry.Name}' as an effect.");

            _handle = _bridge.Load(_entry.Path);
            if (_handle == IntPtr.Zero) {
                string? err = _bridge.LastError();
                throw new InvalidOperationException(
                    $"Failed to load '{_entry.Name}': {err ?? "unknown"}");
            }
            Log.Information($"[VstEffect] Loaded '{_entry.Name}' (0x{_handle:X})");
        }

        public void Setup(double sampleRate, int maxBlockSize) {
            if (_handle == IntPtr.Zero || _isSetup) return;
            if (!_bridge.Setup(_handle, sampleRate, maxBlockSize)) {
                Log.Warning($"[VstEffect] Setup failed: {_bridge.LastError()}");
                return;
            }
            _isSetup = true;
            // Activate is deferred to first Process() call —
            // avoids keeping plugins hot while idle.
        }

        // ── IEffect ────────────────────────────────────────────

        public bool IsBypassed => _slot.Bypassed;
        public void Process(float[] buffer, int offset, int count) {
            if (_slot.Bypassed || _handle == IntPtr.Zero) return;
            EnsureActivated();
            int frames = count / 2;
            if (frames <= 0) return;

            if (offset == 0 && buffer.Length == count) {
                _bridge.Process(_handle, buffer, frames);
            } else {
                float[] slice = new float[count];
                Array.Copy(buffer, offset, slice, 0, count);
                _bridge.Process(_handle, slice, frames);
                Array.Copy(slice, 0, buffer, offset, count);
            }
        }

        private void EnsureActivated() {
            if (_isActivated || !_isSetup) return;
            _bridge.Activate(_handle, true);
            _isActivated = true;
        }
        public void Reset() {
            if (_handle != IntPtr.Zero) _bridge.Reset(_handle);
        }

        // ── Native GUI ─────────────────────────────────────────

        /// <summary>
        /// Open native editor in a Win32 popup window.
        /// Each call creates a new window (multiple windows allowed).
        /// </summary>
        public bool OpenNativeEditor() {
            if (_handle == IntPtr.Zero) return false;
            bool ok = _bridge.OpenEditorWindow(_handle);
            if (ok)
                Log.Information($"[VstEffect] Native editor opened for '{_entry.Name}'");
            else
                Log.Warning($"[VstEffect] Editor failed for '{_entry.Name}': {_bridge.LastError()}");
            return ok;
        }

        // ── State Persistence ──────────────────────────────────

        /// <summary>Save processor state (for .ustxp). Returns null on failure.</summary>
        public byte[]? SaveState() {
            if (_handle == IntPtr.Zero) return null;
            return _bridge.SaveState(_handle);
        }

        /// <summary>Restore processor state (from .ustxp). Returns true on success.</summary>
        public bool RestoreState(byte[]? data) {
            if (_handle == IntPtr.Zero || data == null || data.Length == 0) return false;
            return _bridge.RestoreState(_handle, data);
        }

        // ── IDisposable ────────────────────────────────────────

        public void Dispose() {
            if (_handle != IntPtr.Zero) {
                if (_isActivated) {
                    _bridge.Activate(_handle, false);
                    _isActivated = false;
                }
                _bridge.Unload(_handle);
                Log.Information($"[VstEffect] Disposed '{_entry.Name}' (0x{_handle:X})");
                _handle = IntPtr.Zero;
            }
        }
    }
}
