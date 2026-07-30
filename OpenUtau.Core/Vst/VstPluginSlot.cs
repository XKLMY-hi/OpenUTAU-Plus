using System;
using Serilog;
using YamlDotNet.Serialization;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Per-track VST plugin slot (reference mode).
    /// Stores only a PluginUid (global identity).  Name, path, and type
    /// are resolved at load time from the global VstPluginRegistry.
    /// </summary>
    public class VstPluginSlot {
        /// <summary>Global plugin identity (VST3 CID or VST2 composite key).</summary>
        public string PluginUid { get; set; } = string.Empty;

        /// <summary>Per-slot bypass toggle.</summary>
        public bool Bypassed { get; set; }

        /// <summary>Plugin processor state (VST3 getState). Base64-encoded in .ustxp.</summary>
        [YamlIgnore]
        public byte[]? StateData { get; set; }

        /// <summary>Base64 wrapper for YAML serialization.</summary>
        public string? StateDataBase64 {
            get => StateData != null ? Convert.ToBase64String(StateData) : null;
            set {
                if (string.IsNullOrEmpty(value)) { StateData = null; return; }
                try { StateData = Convert.FromBase64String(value); }
                catch (FormatException ex) {
                    Log.Warning(ex, "VstPluginSlot: corrupt Base64 state — discarding");
                    StateData = null;
                }
            }
        }

        /// <summary>Zero-based slot index on the track.</summary>
        public int SlotIndex { get; set; }

        /// <summary>True when a plugin is assigned to this slot.</summary>
        [YamlIgnore]
        public bool IsLoaded => !string.IsNullOrEmpty(PluginUid);

        /// <summary>Resolved display name from the global registry.</summary>
        [YamlIgnore]
        public string DisplayName =>
            VstPluginRegistry.Inst.TryGet(PluginUid)?.Name ?? (IsLoaded ? PluginUid : string.Empty);

        /// <summary>Local file path from the global registry.</summary>
        [YamlIgnore]
        public string ResolvedPath =>
            VstPluginRegistry.Inst.TryGet(PluginUid)?.Path ?? string.Empty;

        /// <summary>Plugin type badge: VST2, VST3, etc.</summary>
        [YamlIgnore]
        public string PluginTypeDisplay {
            get {
                var e = VstPluginRegistry.Inst.TryGet(PluginUid);
                if (e == null) return "";
                return e.Type switch {
                    VstPluginType.VST3 => e.IsEffect ? "VST3" : "VST3i",
                    VstPluginType.VST2 => e.IsEffect ? "VST2" : "VST2i",
                    _ => "",
                };
            }
        }

        /// <summary>Vendor name from registry.</summary>
        [YamlIgnore]
        public string PluginVendor =>
            VstPluginRegistry.Inst.TryGet(PluginUid)?.Vendor ?? "";

        /// <summary>Underlying VstPluginEntry from registry.</summary>
        [YamlIgnore]
        public VstPluginEntry? Entry =>
            VstPluginRegistry.Inst.TryGet(PluginUid);

        public VstPluginSlot() { }
        public VstPluginSlot(int index) => SlotIndex = index;

        public void Clear() {
            PluginUid = string.Empty;
            Bypassed = false;
        }

        public override string ToString() =>
            IsLoaded ? $"[{SlotIndex}] {DisplayName}" : $"[{SlotIndex}] Empty";
    }
}
