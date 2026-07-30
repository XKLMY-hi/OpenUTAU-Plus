using System.Collections.Generic;
using System.Reflection;

namespace OpenUtau.Core.Vst {
    internal static class VstTestSetup {
        /// <summary>Register a fake entry so VstEffect can resolve its slot's PluginUid.</summary>
        public static void Register(VstPluginEntry entry) {
            var registry = VstPluginRegistry.Inst;
            var field = typeof(VstPluginRegistry).GetField("_entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (Dictionary<string, VstPluginEntry>)field!.GetValue(registry)!;
            dict[entry.Uid] = entry;
        }

        public static VstPluginSlot CreateSlot(string uid, int index = 0) =>
            new(index) { PluginUid = uid };

        public static VstPluginEntry MakeEntry(string uid, string name = "TestPlugin") =>
            new() {
                Uid = uid, Name = name, Vendor = "TestCo",
                Path = "/fake/path.vst3", Type = VstPluginType.VST3,
                IsEffect = true, SubCategories = new List<string> { "Fx" },
            };
    }
}
