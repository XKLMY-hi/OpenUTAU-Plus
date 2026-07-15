using System.Linq;
using OpenUtau.Core.Vst;

Console.WriteLine("=== VST Param Channel + State Test ===\n");
VstPluginRegistry.Inst.ScanAll();

var ott = VstPluginRegistry.Inst.All.First(e => e.Name == "OTT");
Console.WriteLine($"Target: {ott.Name}\n");

// ── Test: setParam → process → verify ──────────────────────
var fx = new VstEffect(new VstPluginSlot { PluginUid = ott.Uid });
fx.Load(); fx.Setup(44100, 4096);

int pid = 2; // In Gain
Console.WriteLine($"Initial param[{pid}] = {VstBridge.GetParam(fx.GetBridgeHandle(), pid):F3}");

// Set via host (simulates GUI performEdit)
VstBridge.SetParam(fx.GetBridgeHandle(), pid, 0.25f);
Console.WriteLine($"After set: param[{pid}] = {VstBridge.GetParam(fx.GetBridgeHandle(), pid):F3}");

// Process
float[] buf = new float[1024];
double rmsB = 0;
for (int i = 0; i < 512; i++) {
    float s = 0.5f * (float)Math.Sin(2*Math.PI*440*i/44100);
    buf[i*2] = s; buf[i*2+1] = s;
    rmsB += s * s;
}
rmsB = Math.Sqrt(rmsB / 512);
fx.Process(buf, 0, 1024);

double rmsA = 0;
for (int i = 0; i < 512; i++) rmsA += buf[i*2] * buf[i*2];
rmsA = Math.Sqrt(rmsA / 512);
Console.WriteLine($"RMS: {rmsB:F4} → {rmsA:F4}");
Console.WriteLine(rmsA < rmsB * 0.9 ? "✅ Param change affected audio!" : "⚠️ Audio unchanged");

// ── State persistence ────────────────────────────────────────
Console.WriteLine($"\nSave state...");
var saved = fx.SaveState();
Console.WriteLine($"  Saved: {saved?.Length ?? 0} bytes");

var slot = new VstPluginSlot { PluginUid = ott.Uid };
slot.StateData = saved;

fx.Dispose();

// Reload + restore
fx = new VstEffect(new VstPluginSlot { PluginUid = ott.Uid });
fx.Load(); fx.Setup(44100, 4096);
Console.WriteLine($"  After reload: param[{pid}] = {VstBridge.GetParam(fx.GetBridgeHandle(), pid):F3}");
fx.RestoreState(slot.StateData);
Console.WriteLine($"  After restore: param[{pid}] = {VstBridge.GetParam(fx.GetBridgeHandle(), pid):F3}");

fx.Dispose();
Console.WriteLine("\n✅ Tests complete");
