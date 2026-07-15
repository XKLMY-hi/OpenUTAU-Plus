using System;
using System.IO;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Lightweight VST2 DLL probe for scanning.
    /// Checks whether a DLL exports VSTPluginMain (or main) to confirm it is a VST2 plugin.
    /// Full VST2 hosting happens via the C++ bridge in the VstEffect layer (Step 4).
    /// </summary>
    internal sealed class Probe : IDisposable {
        private IntPtr _module;
        private IntPtr _entry;

        public bool IsValid => _module != IntPtr.Zero && _entry != IntPtr.Zero;
        public int UniqueId => 0;   // Placeholder — real ID comes from bridge
        public string? Name { get; }

        public Probe(string dllPath) {
            Name = Path.GetFileNameWithoutExtension(dllPath);
            if (!OS.IsWindows()) return;

            _module = LoadLibrary(dllPath);
            if (_module == IntPtr.Zero) return;

            _entry = GetProcAddress(_module, "VSTPluginMain");
            if (_entry == IntPtr.Zero)
                _entry = GetProcAddress(_module, "main");
        }

        public void Dispose() {
            if (_module != IntPtr.Zero) {
                if (OS.IsWindows()) FreeLibrary(_module);
                _module = IntPtr.Zero;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32")]
        static extern IntPtr LoadLibrary(string path);
        [System.Runtime.InteropServices.DllImport("kernel32")]
        static extern IntPtr GetProcAddress(IntPtr mod, string name);
        [System.Runtime.InteropServices.DllImport("kernel32")]
        static extern bool FreeLibrary(IntPtr mod);
    }
}
