using System.Runtime.InteropServices;
using OpenUtau.Core.Vst;

// ── 桥接层 GUI 手动测试 ─────────────────────────────────────────────
// 用法: dotnet run --project VstTest -- --gui "Persistent Q"
// 在 Main 线程: vst_load → vst_open_editor_window(attached 同步) → Main 消息循环
// 验证桥接层本身能否打开大插件 GUI（与应用层线程模型无关）。

if (args.Length >= 2 && args[0] == "--raw") {
    string path = args[1];
    Console.WriteLine($"=== Bridge RAW GUI Test: {path} ===\n");

    // 直接 P/Invoke 桥接层——绕过 registry/VstEffect
    // controller 在 Main 线程创建（vst_load 在 Main 线程）
    IntPtr handle = VstBridge.Load(path);
    if (handle == IntPtr.Zero) {
        Console.WriteLine($"Load FAILED: {VstBridge.LastError()}");
        return;
    }
    Console.WriteLine("Loaded OK. Opening editor (attached on Main thread)...");

    uint t0 = Native.GetTickCount();
    bool ok = VstBridge.OpenEditorWindow(handle);
    uint t1 = Native.GetTickCount();
    Console.WriteLine($"OpenEditorWindow: {ok}  ({t1 - t0} ms)");
    if (!ok) {
        Console.WriteLine($"Error: {VstBridge.LastError()}");
        VstBridge.Unload(handle);
        return;
    }

    // Main 线程消息循环泵编辑器窗口（标准 Win32 宿主模式）
    Console.WriteLine("Message loop running 15s (window should be interactive)...");
    uint start = Native.GetTickCount();
    uint elapsed = 15000;
    while (Native.GetTickCount() - start < 15000) {
        if (Native.GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) == 0) { elapsed = Native.GetTickCount() - start; break; }
        Native.TranslateMessage(ref msg);
        Native.DispatchMessageW(ref msg);
    }
    Console.WriteLine($"Loop exited after {elapsed} ms");

    // 关闭 + 卸载（验证 vst_close_editor 的 SendMessageW 同步关闭不卡死）
    Console.WriteLine("Closing editor...");
    t0 = Native.GetTickCount();
    VstBridge.CloseEditor(handle);
    t1 = Native.GetTickCount();
    Console.WriteLine($"CloseEditor: {(t1 - t0)} ms");
    VstBridge.Unload(handle);
    Console.WriteLine("Unloaded — DONE");
    return;
}

if (args.Length >= 1 && args[0] == "--fx-threadtest") {
    Console.WriteLine("=== VstEffect Load/Dispose from background thread ===\n");
    VstPluginRegistry.Inst.ScanAll();
    var entry = VstPluginRegistry.Inst.All.First(e => e.Name == "OTT");
    var task = Task.Run(() => {
        var fx = new VstEffect(new VstPluginSlot { PluginUid = entry.Uid });
        fx.Load();
        Console.WriteLine($"Loaded on thread {Environment.CurrentManagedThreadId}");
        fx.Dispose();
        Console.WriteLine("Disposed");
    });
    task.Wait();
    Console.WriteLine("Background thread path OK — DONE");
    return;
}

if (args.Length >= 2 && args[0] == "--vstthread-gui") {
    // VstThread 路径（应用同路径）：load + attached + 消息循环全在专用线程
    Console.WriteLine($"=== VstThread GUI Test: {args[1]} ===\n");
    IntPtr handle = VstBridge.Load(args[1]);
    if (handle == IntPtr.Zero) { Console.WriteLine($"Load FAILED: {VstBridge.LastError()}"); return; }
    Console.WriteLine("Loaded. Creating VstThread...");
    var vt = new VstThread("guittest");
    Console.WriteLine("Opening editor on VST thread...");
    uint t0 = Native.GetTickCount();
    bool ok = vt.Invoke(() => VstBridge.OpenEditorWindow(handle));
    uint t1 = Native.GetTickCount();
    Console.WriteLine($"OpenEditorWindow: {ok} ({t1 - t0} ms)");
    if (!ok) { Console.WriteLine($"Error: {VstBridge.LastError()}"); vt.Dispose(); VstBridge.Unload(handle); return; }
    Console.WriteLine("Window open 30s (check rendering)...");
    Thread.Sleep(30000);
    Console.WriteLine("Closing...");
    vt.Invoke(() => VstBridge.CloseEditor(handle));
    vt.Dispose();
    VstBridge.Unload(handle);
    Console.WriteLine("DONE");
    return;
}

if (args.Length >= 1 && args[0] == "--threadtest2") {
    Console.WriteLine("=== VstThread from background thread ===\n");
    var t2 = new VstThread("t2");
    var task = Task.Run(() => t2.Invoke(() => 42));
    int r2 = task.Result;
    Console.WriteLine($"Background Invoke result: {r2}");
    t2.Dispose();
    Console.WriteLine("Disposed OK — DONE");
    return;
}

if (args.Length >= 1 && args[0] == "--threadtest") {
    Console.WriteLine("=== VstThread smoke test ===\n");
    var t = new VstThread("smoke");
    Console.WriteLine($"Thread created. Invoke 42...");
    int r = t.Invoke(() => 42);
    Console.WriteLine($"Invoke result: {r}");
    t.Invoke(() => Console.WriteLine("action executed on vst thread"));
    Console.WriteLine($"Disposing...");
    t.Dispose();
    Console.WriteLine("Disposed OK — DONE");
    return;
}

if (args.Length >= 2 && args[0] == "--gui") {
    string pluginName = args[1];
    Console.WriteLine($"=== Bridge GUI Test: {pluginName} ===\n");

    VstPluginRegistry.Inst.ScanAll();
    var entry = VstPluginRegistry.Inst.All.FirstOrDefault(e => e.Name == pluginName)
        ?? throw new Exception($"Plugin '{pluginName}' not found in scan");
    Console.WriteLine($"Path: {entry.Path}\n");

    // controller 在 Main 线程创建（vst_load 在 Main 线程）
    var fx = new VstEffect(new VstPluginSlot { PluginUid = entry.Uid });
    fx.Load();
    Console.WriteLine("Loaded OK. Opening editor (attached on Main thread)...");

    uint t0 = Native.GetTickCount();
    bool ok = fx.OpenNativeEditor();
    uint t1 = Native.GetTickCount();
    Console.WriteLine($"OpenEditorWindow: {ok}  ({t1 - t0} ms)");
    if (!ok) {
        Console.WriteLine($"Error: {VstBridge.LastError()}");
        fx.Dispose();
        return;
    }

    // Main 线程消息循环泵编辑器窗口（标准 Win32 宿主模式）
    Console.WriteLine("Message loop running 15s (window should be interactive)...");
    uint start = Native.GetTickCount();
    uint elapsed = 15000;
    while (Native.GetTickCount() - start < 15000) {
        if (Native.GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) == 0) { elapsed = Native.GetTickCount() - start; break; }
        Native.TranslateMessage(ref msg);
        Native.DispatchMessageW(ref msg);
    }
    Console.WriteLine($"Loop exited after {elapsed} ms");

    // 关闭 + 卸载（验证 vst_close_editor 的 SendMessageW 同步关闭不卡死）
    Console.WriteLine("Closing editor...");
    t0 = Native.GetTickCount();
    fx.Dispose();
    t1 = Native.GetTickCount();
    Console.WriteLine($"Close+Unload: {(t1 - t0)} ms — DONE");
    return;
}

// ── 原有参数通道测试 ─────────────────────────────────────────────────
Console.WriteLine("=== VST Param Channel + State Test ===\n");
VstPluginRegistry.Inst.ScanAll();

var ott = VstPluginRegistry.Inst.All.First(e => e.Name == "OTT");
Console.WriteLine($"Target: {ott.Name}\n");

// —— Test: setParam → process → verify ──────────────────────
var fx2 = new VstEffect(new VstPluginSlot { PluginUid = ott.Uid });
fx2.Load(); fx2.Setup(44100, 4096);

int pid = 2; // In Gain
Console.WriteLine($"Initial param[{pid}] = {VstBridge.GetParam(fx2.GetBridgeHandle(), pid):F3}");

// ── 类型声明（顶级语句之后） ─────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
struct MSG {
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public int pt_x, pt_y;
}

static class Native {
    [DllImport("user32.dll")] public static extern int GetMessageW(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("kernel32.dll")] public static extern uint GetTickCount();
}
