using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using K4os.Hash.xxHash;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// Global plugin registry — cross-project, persisted in Preferences.
    ///
    /// Key design: plugins are identified by a stable UID, NOT by file path.
    ///   VST3 → CID from moduleinfo.json (32-char hex GUID)
    ///   VST2 → "vst2:{hash}" (composite key)
    ///
    /// Category filtering:
    ///   SubCategories from moduleinfo.json classify VST3 as Fx or Instrument.
    ///   Instrument VSTs (synth/sampler/etc.) are NOT offered in the effect rack —
    ///   they have no audio input and would crash the processing chain.
    /// </summary>
    public class VstPluginRegistry {
        public static VstPluginRegistry Inst { get; } = new();

        /// <summary>Test seam: inject a fake bridge for unit-testing VST scanning.</summary>
        public IVstBridge Bridge { get; set; } = RealVstBridge.Instance;

        private readonly Dictionary<string, VstPluginEntry> _entries = new();
        private bool _scanned;

        private VstPluginRegistry() => LoadFromPreferences();

        public VstPluginEntry? TryGet(string? uid) {
            if (string.IsNullOrEmpty(uid)) return null;
            return _entries.TryGetValue(uid, out var e) ? e : null;
        }

        /// <summary>All registered plugins.</summary>
        public IReadOnlyList<VstPluginEntry> All => _entries.Values.OrderBy(e => e.Name).ToList();

        /// <summary>Only effect-type plugins (safe to load in the effect chain).</summary>
        public IReadOnlyList<VstPluginEntry> Effects =>
            _entries.Values.Where(e => e.IsEffect).OrderBy(e => e.Name).ToList();

        public int Count => _entries.Count;
        public int EffectCount => Effects.Count;

        public void ScanAll() {
            if (_scanned) return;
            _scanned = true;
            _entries.Clear();

            var paths = new List<string>();
            paths.AddRange(DefaultVst3Paths);
            paths.AddRange(DefaultVst2Paths);
            if (Preferences.Default.VstScanPaths?.Count > 0)
                paths.AddRange(Preferences.Default.VstScanPaths);

            int found = 0;
            foreach (var path in paths.Distinct()) {
                if (!Directory.Exists(path)) continue;
                // Default VST3 paths: only scan top-level single-file .vst3
                // (avoids loading huge plugins in vendor subdirectories)
                bool isDefaultPath = DefaultVst3Paths.Contains(path);
                found += ScanDirectory(path, isDefaultPath ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
            }
            SaveToPreferences();
            Log.Information($"[VST] Registry: {found} plugins ({Effects.Count} effects)");
        }

        public void Rescan() { _entries.Clear(); _scanned = false; ScanAll(); }

        // ── Scanning ────────────────────────────────────────────

        private int ScanDirectory(string dir, SearchOption searchOpt) {
            int count = 0;
            try {
                foreach (var vst3Dir in Directory.GetDirectories(dir, "*.vst3", searchOpt)) {
                    try { if (ScanVst3Bundle(vst3Dir)) count++; }
                    catch (Exception ex) { Log.Warning($"[VST] Error {vst3Dir}: {ex.Message}"); }
                }
                foreach (var vst3File in Directory.GetFiles(dir, "*.vst3", searchOpt)) {
                    try { if (ScanVst3SingleFile(vst3File)) count++; }
                    catch (Exception ex) { Log.Warning($"[VST] Error {vst3File}: {ex.Message}"); }
                }
                foreach (var dll in Directory.GetFiles(dir, "*.dll", searchOpt)) {
                    try { if (ScanVst2Dll(dll)) count++; }
                    catch { }
                }
            } catch (Exception ex) { Log.Warning($"[VST] Failed {dir}: {ex.Message}"); }
            return count;
        }

        private bool ScanVst3Bundle(string bundleDir) {
            var path = Path.Combine(bundleDir, "Contents", "Resources", "moduleinfo.json");
            if (!File.Exists(path)) return false;
            try {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string name = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "Unknown" : "Unknown";
                string vendor = "";
                if (root.TryGetProperty("Factory Info", out var fi) && fi.TryGetProperty("Vendor", out var v))
                    vendor = v.GetString() ?? "";

                if (root.TryGetProperty("Classes", out var classes)) {
                    foreach (var cls in classes.EnumerateArray()) {
                        string? cid = cls.TryGetProperty("CID", out var c) ? c.GetString() : null;
                        string? cat = cls.TryGetProperty("Category", out var ca) ? ca.GetString() : null;
                        string? cn = cls.TryGetProperty("Name", out var cn1) ? cn1.GetString() : null;
                        if (cat != "Audio Module Class" || string.IsNullOrEmpty(cid)) continue;

                        // Parse sub categories
                        var subs = new List<string>();
                        if (cls.TryGetProperty("Sub Categories", out var sc)) {
                            foreach (var s in sc.EnumerateArray()) {
                                string? sub = s.GetString();
                                if (!string.IsNullOrEmpty(sub)) subs.Add(sub);
                            }
                        }

                        string uid = $"vst3:{NormalizeCid(cid)}";
                        _entries[uid] = new VstPluginEntry {
                            Uid = uid, Name = cn ?? name, Vendor = vendor, Path = bundleDir,
                            Type = VstPluginType.VST3,
                            SubCategories = subs,
                            IsEffect = ClassifyEffect(subs, VstPluginType.VST3),
                        };
                        return true;
                    }
                }
            } catch { }
            return false;
        }

        /// <summary>
        /// Probe a single-file .vst3 DLL (not a bundle directory).
        /// Uses the native vst_probe() which only reads factory metadata
        /// without creating a component — safe for instrument VSTs.
        /// </summary>
        private bool ScanVst3SingleFile(string filePath) {
            // Skip if file is actually a directory
            if (Directory.Exists(filePath)) return false;

            // Skip huge plugins (>100MB) during auto-scan to avoid hangs.
            // Users can still manually load them via the plugin browser.
            try {
                var fi = new System.IO.FileInfo(filePath);
                if (fi.Length > 100 * 1024 * 1024) {
                    Log.Information($"[VST] Skip large plugin: {Path.GetFileName(filePath)} ({fi.Length / 1024 / 1024}MB)");
                    return false;
                }
            } catch { }

            try {
                string? json = VstBridge.Probe(filePath);
                if (string.IsNullOrEmpty(json)) return false;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("classes", out var classes)) return false;
                if (classes.GetArrayLength() == 0) return false;

                // Use class name as plugin name; module name is blank for single-file
                string pluginName = "Unknown";
                string pluginVendor = "";

                bool added = false;
                foreach (var cls in classes.EnumerateArray()) {
                    string? cat = cls.TryGetProperty("category", out var c) ? c.GetString() : null;
                    if (cat != "Audio Module Class") continue;

                    string? cid = cls.TryGetProperty("cid", out var cidEl) ? cidEl.GetString() : null;
                    if (string.IsNullOrEmpty(cid)) continue;

                    string? cn = cls.TryGetProperty("name", out var nm) ? nm.GetString() : null;
                    string? vn = cls.TryGetProperty("vendor", out var v) ? v.GetString() : null;
                    if (!string.IsNullOrEmpty(cn)) pluginName = cn;
                    if (!string.IsNullOrEmpty(vn)) pluginVendor = vn;

                    var subs = new List<string>();
                    if (cls.TryGetProperty("subs", out var sc)) {
                        foreach (var s in sc.EnumerateArray()) {
                            string? sub = s.GetString();
                            if (!string.IsNullOrEmpty(sub)) subs.Add(sub);
                        }
                    }

                    // Normalize CID: lowercase hex, keep only hex chars
                    string normCid = NormalizeCid(cid);
                    string uid = $"vst3:{normCid}";

                    _entries[uid] = new VstPluginEntry {
                        Uid = uid, Name = pluginName, Vendor = pluginVendor,
                        Path = filePath, Type = VstPluginType.VST3,
                        SubCategories = subs,
                        IsEffect = ClassifyEffect(subs, VstPluginType.VST3),
                    };
                    added = true;
                }
                return added;
            } catch (Exception ex) {
                Log.Warning($"[VST] Probe failed {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Normalize a CID to lowercase hex with no dashes.
        /// Handles both GUID formats (with dashes) and raw hex strings.
        /// </summary>
        private static string NormalizeCid(string cid) {
            // Keep only hex chars, lowercase
            var sb = new System.Text.StringBuilder();
            foreach (char ch in cid) {
                if (ch >= '0' && ch <= '9') sb.Append(ch);
                else if (ch >= 'a' && ch <= 'f') sb.Append(ch);
                else if (ch >= 'A' && ch <= 'F') sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private bool ScanVst2Dll(string dllPath) {
            try {
                // ── B4: Process-isolated probe ──
                // Launch a subprocess to safely call VSTPluginMain() and read
                // AEffect.flags (effFlagsIsSynth). If the DLL crashes, only the
                // subprocess dies — OpenUTAU keeps running.
                var isolated = VstProbeProcess.ProbeVst2(dllPath);
                if (isolated != null) {
                    _entries[isolated.Uid] = isolated;
                    return true;
                }

                // ── Fallback: lightweight probe (in-process, no execution) ──
                using var probe = new Probe(dllPath);
                if (!probe.IsValid) return false;
                string dllName = Path.GetFileNameWithoutExtension(dllPath);
                string uid = BuildVst2Uid(dllName);
                _entries[uid] = new VstPluginEntry {
                    Uid = uid, Name = dllName, Vendor = "", Path = dllPath,
                    Type = VstPluginType.VST2,
                    SubCategories = new List<string>(),
                    IsEffect = true, // conservative fallback
                };
                return true;
            } catch { return false; }
        }

        /// <summary>
        /// Build a stable, cross-machine UID for a VST2 dll from its file name.
        /// Uses xxHash (deterministic across runs/machines) — NOT string.GetHashCode
        /// which is non-deterministic in .NET Core+ and would break .ustxp portability.
        /// </summary>
        internal static string BuildVst2Uid(string dllName) {
            ulong hash = XXH64.DigestOf(Encoding.UTF8.GetBytes(dllName));
            return $"vst2:{hash:x16}";
        }

        /// <summary>
        /// Classify whether this is an audio effect (safe for the processing chain)
        /// or an instrument (has no audio input — would crash or produce silence).
        /// </summary>
        public static bool ClassifyEffect(IReadOnlyList<string> subs, VstPluginType type) {
            // VST2: assume effect (safe default)
            if (type == VstPluginType.VST2) return true;

            // Instrument keywords — these plugins have NO audio input
            var instruments = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Instrument", "Synth", "Sampler", "Drum Machine", "Arpeggiator",
                "Generator", "Tone Generator",
            };

            foreach (var sub in subs) {
                if (instruments.Contains(sub)) return false;
            }

            // If no subcategories at all, assume effect (safe default)
            return true;
        }

        // ── Preferences ─────────────────────────────────────────

        private void LoadFromPreferences() {
            // Restore from cache if available
            if (Preferences.Default.VstCachedPlugins?.Count > 0) {
                foreach (var c in Preferences.Default.VstCachedPlugins) {
                    _entries[c.Uid] = new VstPluginEntry {
                        Uid = c.Uid, Name = c.Name, Vendor = c.Vendor,
                        Path = c.Path, Type = (VstPluginType)c.Type,
                        SubCategories = c.Subs ?? new List<string>(),
                        IsEffect = ClassifyEffect(c.Subs ?? new List<string>(), (VstPluginType)c.Type),
                    };
                }
                _scanned = true;
            }
        }

        private void SaveToPreferences() {
            Preferences.Default.VstCachedPlugins = _entries.Values.Select(e =>
                new Preferences.VstCachedEntry {
                    Uid = e.Uid, Name = e.Name, Vendor = e.Vendor,
                    Path = e.Path, Type = (int)e.Type,
                    Subs = e.SubCategories.ToList(),
                }).ToList();
            Preferences.Save();
        }

        static readonly string[] DefaultVst3Paths = {
            @"C:\Program Files\Common Files\VST3",
            @"C:\Program Files (x86)\Common Files\VST3",
        };
        static readonly string[] DefaultVst2Paths = {
            @"C:\Program Files\VSTPlugins",
            @"C:\Program Files (x86)\VSTPlugins",
            @"C:\Program Files\Steinberg\VSTPlugins",
        };
    }

    // ── Data types ──────────────────────────────────────────

    public class VstPluginEntry {
        public string Uid { get; set; } = "";
        public string Name { get; set; } = "";
        public string Vendor { get; set; } = "";
        public string Path { get; set; } = "";
        public VstPluginType Type { get; set; }
        public List<string> SubCategories { get; set; } = new();
        public bool IsEffect { get; set; } = true;

        public string CategoryDisplay =>
            SubCategories.Count > 0 ? string.Join(", ", SubCategories) : Type.ToString();

        public override string ToString() =>
            IsEffect ? $"[{Type}] {Name}{(Vendor.Length > 0 ? $" — {Vendor}" : "")}"
                     : $"[{Type} Instrument] {Name}{(Vendor.Length > 0 ? $" — {Vendor}" : "")}";
    }

    public enum VstPluginType { Unknown = 0, VST2 = 2, VST3 = 3 }
}
