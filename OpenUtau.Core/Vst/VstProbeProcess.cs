using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Serilog;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Launches vst_probe.exe in a subprocess so that VST DLL code crashes
    /// (DllMain / VSTPluginMain / AEffect access violations) don't bring down
    /// the main OpenUTAU process.  stdout JSON is parsed and returned.
    /// </summary>
    internal static class VstProbeProcess {
        private static string? _exePath;

        static string GetExePath() {
            if (_exePath != null && File.Exists(_exePath)) return _exePath;
            var dir = Path.GetDirectoryName(typeof(VstProbeProcess).Assembly.Location) ?? ".";
            _exePath = Path.Combine(dir, "vst_probe.exe");
            if (!File.Exists(_exePath)) {
                // Fallback: search runtimes/win-x64/native (dev layout)
                _exePath = Path.Combine(dir, "runtimes", "win-x64", "native", "vst_probe.exe");
            }
            return _exePath;
        }

        /// <summary>
        /// Probe a VST2 DLL in a subprocess.  Returns null on failure,
        /// otherwise a populated entry with IsEffect determined by the probe.
        /// </summary>
        public static VstPluginEntry? ProbeVst2(string dllPath) {
            if (!OS.IsWindows()) return null;

            string exe = GetExePath();
            if (!File.Exists(exe)) {
                Log.Warning($"[VstProbe] exe not found: {exe}");
                return null;
            }

            try {
                using var proc = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = exe,
                        Arguments = $"\"{dllPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };

                proc.Start();
                string stdout = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000); // 5s timeout

                if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                    return null;

                using var doc = JsonDocument.Parse(stdout);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out _)) return null;

                string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                bool isEffect = root.TryGetProperty("isEffect", out var ie) && ie.GetBoolean();

                string dllName = Path.GetFileNameWithoutExtension(dllPath);
                string uid = VstPluginRegistry.BuildVst2Uid(dllName);
                return new VstPluginEntry {
                    Uid = uid, Name = name.Length > 0 ? name : dllName,
                    Vendor = "", Path = dllPath,
                    Type = VstPluginType.VST2,
                    SubCategories = new System.Collections.Generic.List<string>(),
                    IsEffect = isEffect,
                };
            } catch (Exception ex) {
                Log.Warning($"[VstProbe] Subprocess failed for '{dllPath}': {ex.Message}");
                return null;
            }
        }
    }
}
