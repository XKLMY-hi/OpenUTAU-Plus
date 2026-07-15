using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Compatibility facade that delegates to VstPluginRegistry.
    /// Kept around so existing UI code compiles; will be phased out
    /// when Step #27 (Unified Track Effect Rack) replaces VstRackWindow.
    /// </summary>
    public class VstPluginManager {
        private static VstPluginManager? _inst;
        public static VstPluginManager Inst => _inst ??= new();

        /// <summary>Plugin info from the global registry.</summary>
        public Dictionary<string, VstPluginInfo> KnownPlugins => VstPluginRegistry.Inst.All
            .ToDictionary(e => e.Uid, e => new VstPluginInfo {
                PluginUid = e.Uid,
                PluginName = e.Name,
                PluginPath = e.Path,
                PluginType = e.Type,
                Vendor = e.Vendor,
            });

        public List<VstPluginInfo> ScanPlugins() {
            VstPluginRegistry.Inst.Rescan();
            return KnownPlugins.Values.ToList();
        }

        public bool LoadPlugin(VstPluginSlot slot) {
            if (string.IsNullOrEmpty(slot.PluginUid)) return false;
            var entry = VstPluginRegistry.Inst.TryGet(slot.PluginUid);
            if (entry == null) return false;
            Log.Information($"[VST] Load stub: {entry.Name} ({entry.Uid})");
            return true;
        }

        public void UnloadPlugin(VstPluginSlot slot) {
            Log.Information($"[VST] Unload: {slot.PluginUid}");
        }

        public static List<VstPluginSlot> CreateDefaultSlots(int count = 3) {
            var slots = new List<VstPluginSlot>();
            for (int i = 0; i < count; i++)
                slots.Add(new VstPluginSlot(i));
            return slots;
        }
    }

    /// <summary>Legacy compatibility type.  Use VstPluginEntry for new code.</summary>
    public class VstPluginInfo {
        public string PluginUid { get; set; } = string.Empty;
        public string PluginName { get; set; } = string.Empty;
        public string PluginPath { get; set; } = string.Empty;
        public VstPluginType PluginType { get; set; }
        public string Vendor { get; set; } = string.Empty;
        public List<string> Subs { get; set; } = new();
        public bool IsEffect { get; set; } = true;
        public override string ToString() => $"[{PluginType}] {PluginName}";
    }
}
