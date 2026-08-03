using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Global VST instance registry.
    /// UI thread: LoadEffect / UnloadEffect.
    /// Audio thread: GetActiveEffects (lock-free read via volatile).
    /// </summary>
    public class VstPluginManager {
        private static VstPluginManager? _inst;
        public static VstPluginManager Inst => _inst ??= new();

        /// <summary>Test seam: inject a fake bridge for unit-testing VST lifecycle.</summary>
        public IVstBridge Bridge { get; set; } = RealVstBridge.Instance;

        private readonly object _lock = new();
        private readonly Dictionary<int, VstTrackInstances> _tracks = new();

        // ── Scanning ────────────────────────────────────────────

        public List<VstPluginInfo> ScanPlugins() {
            VstPluginRegistry.Inst.Rescan();
            return KnownPlugins.Values.ToList();
        }
        public Dictionary<string, VstPluginInfo> KnownPlugins => VstPluginRegistry.Inst.All
            .ToDictionary(e => e.Uid, e => new VstPluginInfo {
                PluginUid = e.Uid, PluginName = e.Name, PluginPath = e.Path,
                PluginType = e.Type, Vendor = e.Vendor,
                Subs = e.SubCategories.ToList(), IsEffect = e.IsEffect,
            });

        // ── Track instances ─────────────────────────────────────

        VstTrackInstances GetOrCreate(int trackNo) {
            lock (_lock) {
                if (!_tracks.TryGetValue(trackNo, out var ti)) {
                    ti = new VstTrackInstances(trackNo) { Bridge = Bridge };
                    _tracks[trackNo] = ti;
                } else {
                    ti.Bridge = Bridge;
                }
                return ti;
            }
        }

        /// <summary>Load/reload a VST effect. Called from UI thread.</summary>
        public VstEffect? LoadEffect(int trackNo, VstPluginSlot slot) {
            var ti = GetOrCreate(trackNo);
            return ti.LoadAt(slot.SlotIndex, slot);
        }

        /// <summary>Unload an effect. Called from UI thread.</summary>
        public void UnloadEffect(int trackNo, int slotIndex) {
            if (_tracks.TryGetValue(trackNo, out var ti))
                ti.UnloadAt(slotIndex);
        }

        /// <summary>Get active effects for a track (audio thread safe).</summary>
        public IReadOnlyList<VstEffect> GetActiveEffects(int trackNo) {
            if (_tracks.TryGetValue(trackNo, out var ti))
                return ti.GetActiveEffects();
            return System.Array.Empty<VstEffect>();
        }

        /// <summary>Get a specific slot's effect.</summary>
        public VstEffect? GetEffect(int trackNo, int slotIndex) {
            if (_tracks.TryGetValue(trackNo, out var ti))
                return ti[slotIndex];
            return null;
        }

        /// <summary>Save state for all active effects on a track.</summary>
        public void SaveAllStates(int trackNo) {
            if (!_tracks.TryGetValue(trackNo, out var ti)) return;
            var all = ti.GetActiveEffects();
            foreach (var fx in all) {
                var state = fx.SaveState();
                if (state != null) fx.Slot.StateData = state;
            }
        }

        public void RemoveTrack(int trackNo) {
            lock (_lock) {
                if (_tracks.TryGetValue(trackNo, out var ti)) {
                    ti.Dispose(); _tracks.Remove(trackNo);
                }
            }
        }
        /// <summary>Safe-dispose all effects queued for removal on all tracks.
        /// Call from RenderEngine before building the next render cycle.</summary>
        public void FlushAllPendingDispose() {
            lock (_lock) {
                foreach (var kv in _tracks) kv.Value.FlushPendingDispose();
            }
        }

        public void ClearAll() {
            lock (_lock) {
                foreach (var kv in _tracks) kv.Value.Dispose();
                _tracks.Clear();
            }
        }
        public static List<VstPluginSlot> CreateDefaultSlots(int count = 3) {
            var s = new List<VstPluginSlot>();
            for (int i = 0; i < count; i++) s.Add(new VstPluginSlot(i));
            return s;
        }
    }

    public class VstPluginInfo {
        public string PluginUid { get; set; } = "";
        public string PluginName { get; set; } = "";
        public string PluginPath { get; set; } = "";
        public VstPluginType PluginType { get; set; }
        public string Vendor { get; set; } = "";
        public List<string> Subs { get; set; } = new();
        public bool IsEffect { get; set; } = true;
        public override string ToString() => $"[{PluginType}] {PluginName}";
    }
}
