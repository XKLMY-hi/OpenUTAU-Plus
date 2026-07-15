using System.Linq;
using OpenUtau.Core.Vst;

Console.WriteLine("=== VST Param Channel + State Test ===\n");
VstPluginRegistry.Inst.ScanAll();

var ott = VstPluginRegistry.Inst.All.First(e => e.Name == "OTT");
Console.WriteLine($"Target: {ott.Name}\n");

// —— Test: setParam → process → verify ──────────────────────
var fx = new VstEffect(new VstPluginSlot { PluginUid = ott.Uid });
fx.Load(); fx.Setup(44100, 4096);

int pid = 2; // In Gain
Console.WriteLine($"Initial param[{pid}] = {VstBridge.GetParam(fx.GetBridgeHandle(), pid):F3}");
