using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Colors;
public class CustomTheme {
    public static Dictionary<string, string> Themes = [];
    static HashSet<string> LocalThemes = [];
    static HashSet<string> PackageThemes = [];
    public static ThemeYaml Default;

    public static bool IsPackageTheme(string themeName) => PackageThemes.Contains(themeName);

    public static void MarkPackageTheme(string themeName) => PackageThemes.Add(themeName);

    internal static void ClearPackageThemes() {
        foreach (var key in PackageThemes) Themes.Remove(key);
        PackageThemes.Clear();
    }

    static CustomTheme() {
        Default = new ThemeYaml();
        ListThemes();
    }

    public static void Load(string themeName) {
        if (!string.IsNullOrEmpty(themeName) && Themes.TryGetValue(themeName, out var themePath) && File.Exists(themePath)) {
            try {
                Default = Yaml.DefaultDeserializer.Deserialize<ThemeYaml>(File.ReadAllText(themePath, Encoding.UTF8));
                return;
            } catch (Exception e) {
                Log.Error(e, $"Failed to parse yaml in {themePath}");
            }
        }

        Preferences.Default.ThemeName = "Light";
        Default = new ThemeYaml();
    }

    public static void ListThemes() {
        foreach (var key in LocalThemes) Themes.Remove(key);
        LocalThemes.Clear();
        Directory.CreateDirectory(PathManager.Inst.ThemesPath);
        foreach (var item in Directory.EnumerateFiles(PathManager.Inst.ThemesPath, "*.yaml")) {
            try {
                string baseName = Yaml.DefaultDeserializer.Deserialize<ThemeYaml>(File.ReadAllText(item, Encoding.UTF8)).Name;
                string themeName = baseName;
                int dupIter = 1;
                while (Themes.ContainsKey(themeName)) {
                    themeName = $"{baseName} ({dupIter})";
                    dupIter++;
                }
                Themes.Add(themeName, item);
                LocalThemes.Add(themeName);
            } catch (Exception e) {
                Log.Error(e, $"Failed to parse yaml in {item}");
            }
        }
    }

    private static readonly Dictionary<string, ThemeVariant> Variants = [];

    /// <summary>
    /// 注册自定义 YAML 主题为 ThemeVariant 并挂 palette 到 ThemeDictionaries。
    /// InheritVariant 按 IsDarkMode 取 Dark/Light：未显式覆盖的键回退到对应内置主题 → Default。
    /// </summary>
    public static ThemeVariant RegisterVariant(string themeName) {
        Load(themeName);
        if (Variants.TryGetValue(themeName, out var existing)) {
            return existing;
        }
        var variant = new ThemeVariant(themeName, Default.IsDarkMode == true ? ThemeVariant.Dark : ThemeVariant.Light);
        Variants[themeName] = variant;
        if (Application.Current != null) {
            var root = (ResourceDictionary)Application.Current.Resources;
            root.ThemeDictionaries[variant] = BuildPalette(Default);
        }
        return variant;
    }

    /// <summary>把 ThemeYaml 全部颜色（legacy 33 键 + Plus* 映射）构建为 palette 字典。供变体注册与测试直接调用。</summary>
    public static ResourceDictionary BuildPalette(ThemeYaml yaml) {
        var dict = new ResourceDictionary();
        dict["IsDarkMode"] = yaml.IsDarkMode;
        void Set(string res, string colorStr) {
            if (Color.TryParse(colorStr, out var color)) {
                dict[res] = color;
            } else {
                Log.Error($"Failed to parse color \"{colorStr}\" in custom theme");
            }
        }
        void SetN(string res, string? colorStr) {
            if (!string.IsNullOrEmpty(colorStr)) {
                Set(res, colorStr);
            }
        }

        // legacy 33 键
        Set("BackgroundColor", yaml.BackgroundColor);
        Set("BackgroundColorPointerOver", yaml.BackgroundColorPointerOver);
        Set("BackgroundColorPressed", yaml.BackgroundColorPressed);
        Set("BackgroundColorDisabled", yaml.BackgroundColorDisabled);
        Set("ForegroundColor", yaml.ForegroundColor);
        Set("ForegroundColorPointerOver", yaml.ForegroundColorPointerOver);
        Set("ForegroundColorPressed", yaml.ForegroundColorPressed);
        Set("ForegroundColorDisabled", yaml.ForegroundColorDisabled);
        Set("BorderColor", yaml.BorderColor);
        Set("BorderColorPointerOver", yaml.BorderColorPointerOver);
        Set("SystemAccentColor", yaml.SystemAccentColor);
        Set("SystemAccentColorLight1", yaml.SystemAccentColorLight1);
        Set("SystemAccentColorDark1", yaml.SystemAccentColorDark1);
        Set("NeutralAccentColor", yaml.NeutralAccentColor);
        Set("NeutralAccentColorPointerOver", yaml.NeutralAccentColorPointerOver);
        Set("AccentColor1", yaml.AccentColor1);
        Set("AccentColor2", yaml.AccentColor2);
        Set("AccentColor3", yaml.AccentColor3);
        Set("TickLineColor", yaml.TickLineColor);
        Set("BarNumberColor", yaml.BarNumberColor);
        Set("FinalPitchColor", yaml.FinalPitchColor);
        Set("TrackBackgroundAltColor", yaml.TrackBackgroundAltColor);
        Set("WarningColor", yaml.WarningColor);
        Set("WhiteKeyColorLeft", yaml.WhiteKeyColorLeft);
        Set("WhiteKeyColorRight", yaml.WhiteKeyColorRight);
        Set("WhiteKeyNameColor", yaml.WhiteKeyNameColor);
        Set("CenterKeyColorLeft", yaml.CenterKeyColorLeft);
        Set("CenterKeyColorRight", yaml.CenterKeyColorRight);
        Set("CenterKeyNameColor", yaml.CenterKeyNameColor);
        Set("BlackKeyColorLeft", yaml.BlackKeyColorLeft);
        Set("BlackKeyColorRight", yaml.BlackKeyColorRight);
        Set("BlackKeyNameColor", yaml.BlackKeyNameColor);

        // legacy → Plus* 自动映射（旧 33 键 YAML 无缝升级）
        Set("PlusSurfaceBase", yaml.BackgroundColor);
        Set("PlusSurfaceHover", yaml.BackgroundColorPointerOver);
        Set("PlusSurfacePressed", yaml.BackgroundColorPressed);
        Set("PlusSurfaceDisabled", yaml.BackgroundColorDisabled);
        Set("PlusBorderDefault", yaml.BorderColor);
        Set("PlusBorderHover", yaml.BorderColorPointerOver);
        Set("PlusAccent", yaml.SystemAccentColor);
        Set("PlusAccentHover", yaml.SystemAccentColorLight1);
        Set("PlusAccentPressed", yaml.SystemAccentColorDark1);
        Set("PlusTextPrimary", yaml.ForegroundColor);
        Set("PlusTextDisabled", yaml.ForegroundColorDisabled);
        Set("PlusSemanticWarningBg", yaml.WarningColor);

        // 由 accent 推导透明档（muted/soft/glow），保证选中/辉光随主色
        if (Color.TryParse(yaml.SystemAccentColor, out var accentColor)) {
            dict["PlusAccentMuted"] = WithAlpha(accentColor, 0.12);
            dict["PlusAccentSoft"] = WithAlpha(accentColor, 0.20);
            dict["PlusAccentGlow"] = WithAlpha(accentColor, 0.30);
        }

        // 非颜色令牌可主题化（nullable 新字段，缺省走 InheritVariant → Plus.Resources 兜底）
        SetN("PlusSurfaceDeep", yaml.SurfaceDeep);
        SetN("PlusSurfaceRaised", yaml.SurfaceRaised);
        SetN("PlusSurfaceControl", yaml.SurfaceControl);
        SetN("PlusSurfaceOverlay", yaml.SurfaceOverlay);
        SetN("PlusBorderSubtle", yaml.BorderSubtle);
        SetN("PlusTextSecondary", yaml.TextSecondary);
        SetN("PlusSemanticSuccess", yaml.SemanticSuccess);
        SetN("PlusSemanticWarning", yaml.SemanticWarning);
        SetN("PlusSemanticDanger", yaml.SemanticDanger);
        SetN("PlusSemanticInfo", yaml.SemanticInfo);
        SetN("PlusGlassCard", yaml.GlassCard);
        SetN("PlusGlassPopover", yaml.GlassPopover);
        SetN("PlusGlassBackdrop", yaml.GlassBackdrop);

        return dict;
    }

    /// <summary>保持色相，按比例收缩 alpha。</summary>
    private static Color WithAlpha(Color c, double fraction) {
        return new Color((byte)Math.Round(c.A * fraction), c.R, c.G, c.B);
    }

    [Serializable]
    public class ThemeYaml {
        public string Name = "Custom YAML";
            
        public bool IsDarkMode = false;
        public string BackgroundColor = "#FFFFFF";
        public string BackgroundColorPointerOver = "#F0F0F0";
        public string BackgroundColorPressed = "#E0E0E0";
        public string BackgroundColorDisabled = "#D0D0D0";

        public string ForegroundColor = "#000000";
        public string ForegroundColorPointerOver = "#000000";
        public string ForegroundColorPressed = "#202020";
        public string ForegroundColorDisabled = "#808080";
        
        public string BorderColor = "#707070";
        public string BorderColorPointerOver = "#B0B0B0";

        public string SystemAccentColor = "#4EA6EA";
        public string SystemAccentColorLight1 = "#90CAF9";
        public string SystemAccentColorDark1 = "#1E88E5";

        public string NeutralAccentColor = "#ADA1B3";
        public string NeutralAccentColorPointerOver = "#948A99";
        public string AccentColor1 = "#4EA6EA";
        public string AccentColor2 = "#FF679D";
        public string AccentColor3 = "#E62E6E";

        public string TickLineColor = "#AFA3B5";
        public string BarNumberColor = "#AFA3B5";
        public string FinalPitchColor = "#C0C0C0";
        public string TrackBackgroundAltColor = "#F0F0F0";
        public string WarningColor = "#FFF4CE";

        public string WhiteKeyColorLeft = "Transparent";
        public string WhiteKeyColorRight = "Transparent";
        public string WhiteKeyNameColor = "#FF347c";
            
        public string CenterKeyColorLeft = "#FFDDE6";
        public string CenterKeyColorRight = "#FFCEDC";
        public string CenterKeyNameColor = "#FF347C";
            
        public string BlackKeyColorLeft = "#FF71A3";
        public string BlackKeyColorRight = "#FF347C";
        public string BlackKeyNameColor = "#FFFFFF";

        // ════════ v4.0 非颜色令牌（全 nullable，缺省=用主题默认走 Plus.Resources 兜底）════════
        // 旧 33 键 YAML 不填这些字段 → legacy→Plus 自动映射已兜住主色。
        public string? SurfaceDeep;
        public string? SurfaceRaised;
        public string? SurfaceControl;
        public string? SurfaceOverlay;
        public string? BorderSubtle;
        public string? TextSecondary;
        public string? SemanticSuccess;
        public string? SemanticWarning;
        public string? SemanticDanger;
        public string? SemanticInfo;
        public string? GlassCard;
        public string? GlassPopover;
        public string? GlassBackdrop;
    }
}

