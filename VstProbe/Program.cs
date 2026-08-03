using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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

    // 与 OpenUtau.Core/Vst/VstBridge.cs 的 vst_probe DllImport 签名一致（LPWStr + Cdecl）
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    delegate int VstProbeFn([MarshalAs(UnmanagedType.LPWStr)] string path, [Out] byte[] jsonBuf, int jsonBufSize);

    static int Main(string[] args) {
        try {
            if (args.Length == 0) { Console.WriteLine("""{"error":"no path"}"""); return 1; }
            if (args[0] == "--vst3") {
                if (args.Length < 2) { Console.WriteLine("""{"error":"no path"}"""); return 1; }
                return ProbeVst3(args[1]);
            }
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

    // ── VST3 单文件探测：委托给 vst_bridge.dll 的 vst_probe（进程外隔离） ──
    // 插件 DLL 崩溃只杀死本探针进程，不影响 OpenUTAU 主进程。
    static int ProbeVst3(string path) {
        if (!File.Exists(path)) { Console.WriteLine("""{"error":"file not found"}"""); return 1; }

        // 发布布局：vst_bridge.dll 与 exe 同目录；Debug 布局：exe 旁 runtimes/win-x64/native
        string[] candidates = {
            Path.Combine(AppContext.BaseDirectory, "vst_bridge.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "vst_bridge.dll"),
        };
        nint mod = 0;
        foreach (var candidate in candidates) {
            if (!File.Exists(candidate)) continue;
            mod = LoadLibrary(candidate);
            if (mod != 0) break;
        }
        if (mod == 0) { Console.WriteLine("""{"error":"vst_bridge.dll not found"}"""); return 1; }

        try {
            nint addr = GetProcAddress(mod, "vst_probe");
            if (addr == 0) { Console.WriteLine("""{"error":"no vst_probe export"}"""); return 1; }

            var fn = Marshal.GetDelegateForFunctionPointer<VstProbeFn>(addr);
            byte[] buf = new byte[8192];
            int r = fn(path, buf, buf.Length);
            if (r != 0) { Console.WriteLine("""{"error":"vst_probe failed"}"""); return 1; }

            int len = 0;
            while (len < buf.Length && buf[len] != 0) len++;
            Console.WriteLine(Encoding.UTF8.GetString(buf, 0, len));
            return 0;
        } catch (Exception ex) {
            Console.WriteLine(JsonSerializer.Serialize(new { error = ex.Message }));
            return 1;
        } finally {
            FreeLibrary(mod);
        }
    }
}
