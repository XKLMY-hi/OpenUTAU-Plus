using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

static class Program {
    const int effFlagsIsSynth = 1 << 4;

    [DllImport("kernel32", SetLastError = true)]
    static extern nint LoadLibrary(string path);
    [DllImport("kernel32", SetLastError = true)]
    static extern nint GetProcAddress(nint mod, string name);
    [DllImport("kernel32")]
    static extern bool FreeLibrary(nint mod);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate nint Vst2EntryPoint(nint hostCallback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate nint HostCallbackProc(nint effect, int opcode, int index, nint value, nint ptr, float opt);

    static nint HostCallbackImpl(nint effect, int opcode, int index, nint value, nint ptr, float opt) => 0;

    static int Main(string[] args) {
        try {
            if (args.Length == 0) { Console.WriteLine("""{"error":"no path"}"""); return 1; }
            string path = args[0];
            if (!File.Exists(path)) { Console.WriteLine("""{"error":"file not found"}"""); return 1; }

            nint mod = LoadLibrary(path);
            if (mod == 0) { Console.WriteLine("""{"error":"LoadLibrary failed"}"""); return 1; }

            try {
                nint addr = GetProcAddress(mod, "VSTPluginMain");
                if (addr == 0) addr = GetProcAddress(mod, "main");
                if (addr == 0) { Console.WriteLine("""{"error":"no entry point"}"""); return 1; }

                var entry = Marshal.GetDelegateForFunctionPointer<Vst2EntryPoint>(addr);
                var cb = Marshal.GetFunctionPointerForDelegate<HostCallbackProc>(HostCallbackImpl);
                nint aeffect = entry(cb);
                if (aeffect == 0) { Console.WriteLine("""{"error":"VSTPluginMain returned null"}"""); return 1; }

                int flags = Marshal.ReadInt32(aeffect, 20);
                bool isSynth = (flags & effFlagsIsSynth) != 0;
                string name = Marshal.PtrToStringAnsi(aeffect + 40, 64)?.TrimEnd('\0') ?? Path.GetFileNameWithoutExtension(path);

                Console.WriteLine(JsonSerializer.Serialize(new { name, isEffect = !isSynth }));
                return 0;
            } finally {
                FreeLibrary(mod);
            }
        } catch (Exception ex) {
            Console.WriteLine(JsonSerializer.Serialize(new { error = ex.Message }));
            return 1;
        }
    }
}
