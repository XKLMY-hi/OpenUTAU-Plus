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
