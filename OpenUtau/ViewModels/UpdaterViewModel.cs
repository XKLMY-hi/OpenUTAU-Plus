using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media;
using Newtonsoft.Json;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using ReactiveUI.Fody.Helpers;
using Serilog;

namespace OpenUtau.App.ViewModels {
    public class UpdaterViewModel : ViewModelBase {
        class GithubRelease {
#pragma warning disable 0649
            public string html_url = string.Empty;
            public long id = long.MaxValue;
            public bool draft;
            public bool prerelease;
            public string tag_name = string.Empty;
            public string name = string.Empty;
#pragma warning restore 0649
        }

        public string AppVersion => $"v{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version}";
        public bool IsDarkMode => ThemeManager.IsDarkMode;
        [Reactive] public string UpdaterStatus { get; set; }
        [Reactive] public bool UpdateAvailable { get; set; }
        [Reactive] public FontWeight UpdateButtonFontWeight { get; set; }
        public Action? CloseApplication { get; set; }

        // Cached result from startup check, consumed by dialog if available.
        private static string? s_latestVersion;
        private static string? s_releaseUrl;
        private string? latestVersion;
        private string? releaseUrl;

        public UpdaterViewModel() {
            UpdaterStatus = string.Empty;
            UpdateAvailable = false;
            UpdateButtonFontWeight = FontWeight.Normal;
        }

        /// <summary>
        /// Check if a newer version exists on the Plus repository.
        /// Called at startup (non-blocking) and possibly again when the dialog opens.
        /// Returns true if an update is available.
        /// </summary>
        public static async Task<bool> CheckForUpdateAsync() {
            try {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "OpenUTAU-Plus");
                client.Timeout = TimeSpan.FromSeconds(15);

                var url = "https://api.github.com/repos/XKLMY-hi/OpenUTAU-Plus/releases";
                Log.Information($"[Updater] Checking releases at {url}");
                using var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string body = await response.Content.ReadAsStringAsync();
                var releases = JsonConvert.DeserializeObject<List<GithubRelease>>(body);
                if (releases == null || releases.Count == 0) {
                    Log.Information("[Updater] No releases found.");
                    return false;
                }

                // Find the latest non-draft release (always include pre-releases for Plus)
                var latest = releases
                    .Where(r => !r.draft)
                    .OrderByDescending(r => r.id)
                    .FirstOrDefault();

                if (latest == null) {
                    Log.Information("[Updater] No eligible release found.");
                    return false;
                }

                Log.Information($"[Updater] Latest release: {latest.tag_name} ({latest.name})");

                // Compare versions. Our tags look like:
                //   "v0.1.568-plus.0.0.1-beta"  →  assembly 0.1.568.1
                //   "0.1.568"                    →  assembly 0.1.568.0
                // Strip leading 'v', extract the base numeric triple, then
                // grab the last numeric segment of the -plus part as revision.
                string latestTag = latest.tag_name.TrimStart('v');
                Version? current = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                if (current == null) return false;

                if (!TryParsePlusVersion(latestTag, out var remoteVer)) {
                    Log.Warning($"[Updater] Cannot parse version tag: {latest.tag_name}");
                    return false;
                }

                bool isNewer = remoteVer > current;
                Log.Information($"[Updater] Current: {current}, Latest: {remoteVer}, Newer: {isNewer}");

                if (isNewer) {
                    s_latestVersion = latest.tag_name;
                    s_releaseUrl = latest.html_url;
                }

                return isNewer;
            } catch (Exception e) {
                Log.Error(e, "[Updater] Failed to check for update.");
                return false;
            }
        }

        /// <summary>
        /// Called when the dialog is shown. If the startup check already
        /// found an update, use that; otherwise run a fresh check.
        /// </summary>
        public async void Init() {
            UpdaterStatus = ThemeManager.GetString("updater.status.checking");

            // If startup check already cached a result, use it
            if (s_latestVersion != null) {
                latestVersion = s_latestVersion;
                releaseUrl = s_releaseUrl;
                UpdaterStatus = string.Format(ThemeManager.GetString("updater.status.available"), latestVersion);
                UpdateAvailable = true;
                UpdateButtonFontWeight = FontWeight.Bold;
                return;
            }

            // Run a fresh check
            bool hasUpdate = await CheckForUpdateAsync();
            if (hasUpdate && s_latestVersion != null) {
                latestVersion = s_latestVersion;
                releaseUrl = s_releaseUrl;
                UpdaterStatus = string.Format(ThemeManager.GetString("updater.status.available"), latestVersion);
                UpdateAvailable = true;
                UpdateButtonFontWeight = FontWeight.Bold;
            } else if (!hasUpdate) {
                UpdaterStatus = ThemeManager.GetString("updater.status.notavailable");
            } else {
                UpdaterStatus = ThemeManager.GetString("updater.status.unknown");
            }
        }

        public void OnGithub() {
            try {
                string url = releaseUrl ?? "https://github.com/XKLMY-hi/OpenUTAU-Plus/releases";
                OS.OpenWeb(url);
            } catch (Exception e) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        public void OnUpdate() {
            // Open the releases page — user downloads manually
            OnGithub();
        }

        public void OnClosing() {
            // No-op: we don't track skipped versions for simplicity
        }

        /// <summary>
        /// Parse a Plus version tag like "0.1.568-plus.0.0.1-beta" or "0.1.568"
        /// into a .NET Version (major.minor.build.revision).
        /// </summary>
        static bool TryParsePlusVersion(string tag, out Version ver) {
            ver = new Version(0, 0);
            // Split off suffix labels (everything after the first alpha segment)
            // "0.1.568-plus.0.0.1-beta" → parts before "-plus" = "0.1.568", then "-plus.0.0.1" → revision=1
            int plusIdx = tag.IndexOf("-plus", StringComparison.OrdinalIgnoreCase);
            string basePart = plusIdx >= 0 ? tag[..plusIdx] : tag;
            int revision = 0;

            if (plusIdx >= 0) {
                string afterPlus = tag[(plusIdx + 5)..]; // skip "-plus"
                if (afterPlus.StartsWith(".")) afterPlus = afterPlus[1..];
                // Extract the last numeric segment as revision
                // "0.0.1-beta" → split by non-digit, last numeric is 1
                int dash = afterPlus.IndexOf('-');
                string numPart = dash >= 0 ? afterPlus[..dash] : afterPlus;
                var segments = numPart.Split('.');
                if (segments.Length > 0 && int.TryParse(segments[^1], out int r))
                    revision = r;
            }

            // Parse the base ("0.1.568") and add revision
            if (!Version.TryParse(basePart, out var baseVer)) return false;
            ver = new Version(
                Math.Max(0, baseVer.Major),
                Math.Max(0, baseVer.Minor),
                Math.Max(0, baseVer.Build >= 0 ? baseVer.Build : 0),
                revision);
            return true;
        }
    }
}
