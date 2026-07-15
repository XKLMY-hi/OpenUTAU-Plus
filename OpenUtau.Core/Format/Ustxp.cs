using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenUtau.Classic;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Core.Format {
    /// <summary>
    /// OpenUTAU Plus native project format (.ustxp).
    /// Extension of the USTX format with additional Plus-specific data:
    /// - Per-track VST plugin chains
    /// - Mixer state
    /// - Plus-only expressions and settings
    ///
    /// Saving: always writes .ustxp (Plus default).
    /// Loading: reads both .ustx (legacy) and .ustxp (native).
    /// </summary>
    public static class Ustxp {
        public static readonly Version kUstxpVersion = new Version(1, 0);

        public const string Extension = ".ustxp";
        public const string LegacyExtension = ".ustx";

        public static bool IsUstxpFile(string path) {
            return path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUstxFile(string path) {
            return path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSupportedFile(string path) {
            return IsUstxpFile(path) || IsUstxFile(path);
        }

        /// <summary>
        /// Save project in .ustxp (Plus native) format.
        /// </summary>
        public static void Save(string filePath, UProject project) {
            try {
                // Ensure .ustxp extension
                if (!filePath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) {
                    filePath = Path.ChangeExtension(filePath, Extension);
                }
                project.ustxVersion = Ustx.kUstxVersion; // Base USTX version for compatibility
                project.ustxpVersion = kUstxpVersion;    // Plus version for Plus-specific tracking
                project.FilePath = filePath;
                project.BeforeSave();
                File.WriteAllText(filePath, Yaml.DefaultSerializer.Serialize(project), Encoding.UTF8);
                project.Saved = true;
                project.AfterSave();
                Preferences.Default.RecoveryPath = string.Empty;
                Preferences.Save();
                DocManager.Inst.Recovered = false;
            } catch (Exception ex) {
                var e = new MessageCustomizableException(
                    $"Failed to save ustxp: {filePath}",
                    $"<translate:errors.failed.save>: {filePath}", ex);
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        /// <summary>
        /// Auto-save for crash recovery. Uses .ustxp extension.
        /// </summary>
        public static void AutoSave(string filePath, UProject project) {
            try {
                // Always save as .ustxp for recovery
                if (!filePath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) &&
                    !filePath.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase)) {
                    filePath = Path.ChangeExtension(filePath, Extension);
                }
                project.ustxVersion = Ustx.kUstxVersion;
                project.ustxpVersion = kUstxpVersion;
                project.BeforeSave();
                File.WriteAllText(filePath, Yaml.DefaultSerializer.Serialize(project), Encoding.UTF8);
                project.AfterSave();
                Preferences.Default.RecoveryPath = filePath;
                Preferences.Save();
            } catch (Exception ex) {
                Log.Error(ex, $"Failed to autosave: {filePath}");
            }
        }

        /// <summary>
        /// Load a project from either .ustxp or .ustx format.
        /// </summary>
        public static UProject Load(string filePath) {
            string text = File.ReadAllText(filePath, Encoding.UTF8);
            UProject project = Yaml.DefaultDeserializer.Deserialize<UProject>(text);

            // Register default expressions
            Ustx.AddDefaultExpressions(project);

            project.FilePath = filePath;
            project.Saved = true;
            project.AfterLoad();
            project.ValidateFull();

            // Version check
            if (project.ustxVersion > kUstxpVersion) {
                throw new MessageCustomizableException(
                    $"Project file is newer than software: {filePath}",
                    $"<translate:errors.failed.opennewerproject>:\n{filePath}",
                    new FileFormatException("Project file is newer than software."));
            }

            // Apply format migrations (same as Ustx.Load)
            if (project.ustxVersion < new Version(0, 4)) {
                MigrateToV04(project);
            }
            if (project.ustxVersion < new Version(0, 5)) {
                MigrateToV05(project);
            }
            if (project.ustxVersion < new Version(0, 6)) {
                MigrateToV06(project);
            }
            if (project.ustxVersion < new Version(0, 7)) {
                MigrateToV07(project);
            }
            if (project.ustxVersion < new Version(0, 9)) {
                MigrateToV09(project);
            }

            // Upgrade USTX base version to latest
            project.ustxVersion = Ustx.kUstxVersion;

            // Run Plus-specific migrations if ustxpVersion is present
            if (project.ustxpVersion != null && project.ustxpVersion < kUstxpVersion) {
                RunPlusMigrations(project, project.ustxpVersion);
            }
            project.ustxpVersion = kUstxpVersion;
            return project;
        }

        /// <summary>
        /// Plus-specific format migrations. Called when ustxpVersion is behind.
        /// </summary>
        private static void RunPlusMigrations(UProject project, Version fromVersion) {
            // v1.0 → future: add Plus migrations here
        }

        private static void MigrateToV04(UProject project) {
            if (project.expressions.TryGetValue("acc", out var exp) && exp.name == "accent") {
                project.expressions.Remove("acc");
                exp.abbr = Ustx.ATK;
                exp.name = "attack";
                project.expressions[Ustx.ATK] = exp;
                project.parts
                    .Where(part => part is UVoicePart)
                    .Select(part => part as UVoicePart)
                    .SelectMany(part => part!.notes)
                    .SelectMany(note => note.phonemeExpressions)
                    .Where(pExp => pExp.abbr == "acc")
                    .ToList()
                    .ForEach(pExp => pExp.abbr = Ustx.ATK);
            }
            project.ValidateFull();
        }

        private static void MigrateToV05(UProject project) {
            project.parts
                .Where(part => part is UVoicePart)
                .Select(part => part as UVoicePart)
                .SelectMany(part => part!.notes)
                .Where(note => note.lyric.StartsWith("..."))
                .ToList()
                .ForEach(note => note.lyric = note.lyric.Replace("...", "+"));
            project.ValidateFull();
        }

        private static void MigrateToV06(UProject project) {
#pragma warning disable CS0612
            project.timeSignatures = new List<UTimeSignature> {
                new UTimeSignature(0, project.beatPerBar, project.beatUnit) };
            project.tempos = new List<UTempo> { new UTempo(0, project.bpm) };
#pragma warning restore CS0612
            project.ValidateFull();
        }

        private static void MigrateToV07(UProject project) {
            var expSelectors = new UProject().expSelectors;
            if (project.expSelectors.Length < expSelectors.Length) {
                for (int i = 0; i < project.expSelectors.Length; i++) {
                    expSelectors[i] = project.expSelectors[i];
                }
                project.expSelectors = expSelectors;
            }
        }

        private static void MigrateToV09(UProject project) {
            // Upgrade to USTX v0.9 level
        }
    }
}
