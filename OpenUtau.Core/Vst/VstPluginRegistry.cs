using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                found += ScanDirectory(path);
            }
            SaveToPreferences();
            Log.Information($"[VST] Registry: {found} plugins ({Effects.Count} effects)");
        }

        public void Rescan() { _entries.Clear(); _scanned = false; ScanAll(); }

        // ── Scanning ────────────────────────────────────────────

        private int ScanDirectory(string dir) {
            int count = 0;
            try {
                foreach (var vst3Dir in Directory.GetDirectories(dir, "*.vst3", SearchOption.AllDirectories)) {
                    try { if (ScanVst3Bundle(vst3Dir)) count++; }
                    catch (Exception ex) { Log.Warning($"[VST] Error {vst3Dir}: {ex.Message}"); }
                }
                foreach (var dll in Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories)) {
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

                        string uid = $"vst3:{cid}";
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

        private bool ScanVst2Dll(string dllPath) {
            try {
                using var probe = new Probe(dllPath);
                if (!probe.IsValid) return false;
                string dllName = Path.GetFileNameWithoutExtension(dllPath);
                string uid = $"vst2:{dllName.GetHashCode():x8}";
                // VST2 has no subcategories — default to effect
                _entries[uid] = new VstPluginEntry {
                    Uid = uid, Name = dllName, Vendor = "", Path = dllPath,
                    Type = VstPluginType.VST2,
                    SubCategories = new List<string>(),
                    IsEffect = true, // VST2 without instrument flags = effect
                };
                return true;
            } catch { return false; }
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
