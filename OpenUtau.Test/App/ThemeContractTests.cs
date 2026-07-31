using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using OpenUtau.App;
using OpenUtau.Colors;
using Xunit;

namespace OpenUtau.Test.App {
    /// <summary>
    /// 主题契约回归门（v4.0 Phase 1）。
    /// 删除 FluentTheme 前后都必须保持：所有 Plus* 令牌 / Fluent 兼容键 / ThemeManager 绑定键可解析。
    /// 缺键即 CI 红，杜绝"新画笔不同步"与"删除 Fluent 后断链"。
    /// </summary>
    public class ThemeContractTests {
        private static readonly string[] PlusColorKeys = {
            // Surface 8
            "PlusSurfaceDeep", "PlusSurfaceBase", "PlusSurfaceRaised", "PlusSurfaceControl",
            "PlusSurfaceHover", "PlusSurfacePressed", "PlusSurfaceDisabled", "PlusSurfaceOverlay",
            // Border 6
            "PlusBorderSubtle", "PlusBorderDefault", "PlusBorderHover", "PlusBorderFocus",
            "PlusBorderGlass", "PlusBorderGlassStrong",
            // Accent 6
            "PlusAccent", "PlusAccentHover", "PlusAccentPressed",
            "PlusAccentMuted", "PlusAccentSoft", "PlusAccentGlow",
            // Text 4
            "PlusTextPrimary", "PlusTextSecondary", "PlusTextDisabled", "PlusTextOnAccent",
            // Semantic 5
            "PlusSemanticSuccess", "PlusSemanticWarning", "PlusSemanticWarningBg",
            "PlusSemanticDanger", "PlusSemanticInfo",
            // Glass 5
            "PlusGlassCard", "PlusGlassPopover", "PlusGlassBackdrop",
            "PlusGlassHeader", "PlusGlassInsetBottom",
        };

        private static readonly string[] PlusBrushKeys = {
            "PlusBrushSurfaceDeep", "PlusBrushSurfaceBase", "PlusBrushSurfaceRaised",
            "PlusBrushSurfaceControl", "PlusBrushSurfaceHover", "PlusBrushSurfacePressed",
            "PlusBrushSurfaceDisabled",
            "PlusBrushBorderSubtle", "PlusBrushBorderDefault", "PlusBrushBorderHover", "PlusBrushBorderFocus",
            "PlusBrushTextPrimary", "PlusBrushTextSecondary", "PlusBrushTextDisabled", "PlusBrushTextOnAccent",
            "PlusBrushAccent", "PlusBrushAccentHover", "PlusBrushAccentPressed",
            "PlusBrushAccentMuted", "PlusBrushAccentSoft",
            "PlusBrushGlassCard", "PlusBrushGlassPopover",
            "PlusBrushSemanticSuccess", "PlusBrushSemanticWarning",
            "PlusBrushSemanticDanger", "PlusBrushSemanticInfo",
        };

        private static readonly string[] PlusGeometryKeys = {
            "PlusRadiusXs", "PlusRadiusSm", "PlusRadiusMd", "PlusRadiusLg", "PlusRadiusXl", "PlusRadiusPill",
            "PlusControlHeight", "PlusControlHeightSmall", "PlusControlHeightLarge", "PlusIconSize",
            "PlusSpace1", "PlusSpace2", "PlusSpace3", "PlusSpace4", "PlusSpace5", "PlusSpace6", "PlusSpace8",
            "PlusFontSizeXs", "PlusFontSizeSm", "PlusFontSizeBase", "PlusFontSizeMd",
            "PlusFontSizeLg", "PlusFontSizeXl", "PlusFontSize2Xl",
            "PlusFontFamily", "PlusFontFamilyMono",
            "PlusElevationContact", "PlusElevationFloat", "PlusElevationRaised", "PlusElevationPopup",
            "PlusHighlightTop", "PlusHighlightTopStrong", "PlusShadowBottom",
            "PlusFocusRing", "PlusGlowPrimary", "PlusGlowDanger",
        };

        /// <summary>删除 FluentTheme 后仍必须本地提供的兼容键（含几何）。</summary>
        private static readonly string[] FluentCompatKeys = {
            "SystemControlForegroundBaseLowBrush", "SystemControlBackgroundAltMediumBrush",
            "MenuFlyoutItemForegroundPressed", "TextControlForegroundDisabled",
            "ComboBoxDropDownBackground", "ComboBoxDropDownBorderBrush",
            "RadioButtonOuterEllipseFill", "RadioButtonOuterEllipseStroke",
            "RadioButtonOuterEllipseFillPointerOver", "RadioButtonOuterEllipseStrokePointerOver",
            "SliderHorizontalThumbWidth", "SliderHorizontalThumbHeight", "ControlContentThemeFontSize",
            "ComboBoxThemeMinWidth", "ComboBoxMinHeight", "ComboBoxPadding",
            "ComboBoxDropdownBorderThickness", "ComboBoxDropdownBorderPadding",
            "AutoCompleteListPadding", "RadioButtonBorderThemeThickness",
        };

        /// <summary>ThemeManager.BrushBindings 投影所依赖的键（缺键会打 WARN 并保留旧值）。</summary>
        private static readonly string[] ThemeManagerBindingKeys = {
            "SystemControlForegroundBaseHighBrush", "SystemControlBackgroundAltHighBrush",
            "NeutralAccentBrush", "NeutralAccentBrushSemi",
            "AccentBrush1", "AccentBrush1Semi", "AccentBrush2", "AccentBrush2Semi",
            "AccentBrush3", "AccentBrush3Semi",
            "TickLineBrushLow", "BarNumberBrush", "FinalPitchBrush",
            "RealCurveFillBrush", "RealCurveStrokeBrush",
        };

        private static bool Resolves(string key) =>
            Application.Current!.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out _);

        [AvaloniaFact]
        public void DarkTheme_AllTokensResolve() {
            ThemeManager.Apply("Dark");

            var all = PlusColorKeys.Concat(PlusBrushKeys)
                .Concat(PlusGeometryKeys).Concat(FluentCompatKeys)
                .Concat(ThemeManagerBindingKeys)
                .ToArray();
            var missing = all.Where(k => !Resolves(k)).ToArray();
            Assert.True(missing.Length == 0, $"缺失资源键: {string.Join(", ", missing)}");
        }

        [AvaloniaFact]
        public void LightTheme_AllTokensResolve() {
            ThemeManager.Apply("Light");

            var all = PlusColorKeys.Concat(PlusBrushKeys)
                .Concat(PlusGeometryKeys).Concat(FluentCompatKeys)
                .Concat(ThemeManagerBindingKeys)
                .ToArray();
            var missing = all.Where(k => !Resolves(k)).ToArray();
            Assert.True(missing.Length == 0, $"缺失资源键: {string.Join(", ", missing)}");
        }

        /// <summary>三态往返：variant、IsDarkMode、静态画刷随切换正确变化且非 null。</summary>
        [AvaloniaFact]
        public void ThemeVariantRoundTrip_DarkLightCustom() {
            ThemeManager.Apply("Dark");
            Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
            Assert.True(ThemeManager.IsDarkMode);
            Assert.NotNull(ThemeManager.AccentBrush1);
            Assert.NotNull(ThemeManager.ForegroundBrush);

            ThemeManager.Apply("Light");
            Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
            Assert.False(ThemeManager.IsDarkMode);
            Assert.NotNull(ThemeManager.AccentBrush1);

            // 自定义 YAML（无文件则回退 Light 基座；注册为独立 ThemeVariant，非内置 Light）
            ThemeManager.Apply("SomeCustom");
            Assert.NotNull(Application.Current!.RequestedThemeVariant);
            Assert.NotEqual(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
            Assert.NotNull(ThemeManager.BackgroundBrush);
        }

        /// <summary>
        /// 切换后解析到的颜色值确实变化（防"切换不完全"回归）：
        /// 变体字典须真正生效，且根字典/Plus.Resources 兜底不得遮蔽变体。
        /// </summary>
        [AvaloniaFact]
        public void ThemeSwitch_ChangesResolvedColorValues() {
            ThemeManager.Apply("Dark");
            Assert.Equal(Color.Parse("#1e1e28"), ResolveValue("BackgroundColor"));
            var tintDark = Assert.IsType<SolidColorBrush>(ResolveValue("AcrylicTintBrush"));
            Assert.Equal(Color.Parse("#D91e2028"), tintDark.Color);

            ThemeManager.Apply("Light");
            Assert.Equal(Color.Parse("#f5f2f0"), ResolveValue("BackgroundColor"));
            var tintLight = Assert.IsType<SolidColorBrush>(ResolveValue("AcrylicTintBrush"));
            Assert.Equal(Color.Parse("#CCf5f2f0"), tintLight.Color);

            // 切回 Dark 确认往返
            ThemeManager.Apply("Dark");
            Assert.Equal(Color.Parse("#1e1e28"), ResolveValue("BackgroundColor"));
        }

        private static object? ResolveValue(string key) {
            Application.Current!.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var v);
            return v;
        }

        /// <summary>诊断：WindowEx 背景画刷 alpha 不得为 0（防"背景全透明"回归）。</summary>
        [AvaloniaFact]
        public void WindowBackground_IsOpaque() {
            ThemeManager.Apply("Dark");
            var win = new OpenUtau.App.Controls.WindowEx();
            Assert.NotNull(win.Background);
            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(win.Background);
            Assert.True(brush.Color.A >= 0xE0, $"Window bg alpha too low: {brush.Color}");
        }

        /// <summary>ChangePianorollColor 不破坏任何投影键。</summary>
        [AvaloniaFact]
        public void ChangePianorollColor_KeepsBindingsResolvable() {
            ThemeManager.Apply("Dark");
            ThemeManager.ChangePianorollColor("Red");

            var missing = ThemeManagerBindingKeys.Where(k => !Resolves(k)).ToArray();
            Assert.Empty(missing);
            Assert.NotNull(ThemeManager.AccentPen1);
        }

        /// <summary>
        /// 向后兼容：旧 33 键 YAML（只填 legacy 字段）经 BuildPalette 自动映射出 Plus* 键，
        /// 且 legacy 主色正确投影到 PlusSurfaceBase/PlusAccent，accent 透明档由主色推导。
        /// </summary>
        [AvaloniaFact]
        public void LegacyThemeYaml_MapsToPlusKeys() {
            ThemeManager.Apply("Dark");  // 基座

            var legacy = new CustomTheme.ThemeYaml {
                IsDarkMode = true,
                BackgroundColor = "#112233",
                BackgroundColorPointerOver = "#223344",
                BackgroundColorPressed = "#334455",
                BackgroundColorDisabled = "#445566",
                SystemAccentColor = "#556677",
                SystemAccentColorLight1 = "#667788",
                SystemAccentColorDark1 = "#778899",
                ForegroundColor = "#8899aa",
                ForegroundColorDisabled = "#99aabb",
                WarningColor = "#aabbcc",
            };
            var palette = CustomTheme.BuildPalette(legacy);

            // Plus* 映射存在
            Assert.True(palette.ContainsKey("PlusSurfaceBase"));
            Assert.True(palette.ContainsKey("PlusSurfaceHover"));
            Assert.True(palette.ContainsKey("PlusAccent"));
            Assert.True(palette.ContainsKey("PlusAccentMuted"));
            Assert.True(palette.ContainsKey("PlusTextPrimary"));
            Assert.True(palette.ContainsKey("PlusSemanticWarningBg"));
            // 映射值正确
            Assert.Equal(Color.Parse("#112233"), palette["PlusSurfaceBase"]);
            Assert.Equal(Color.Parse("#556677"), palette["PlusAccent"]);
            Assert.Equal(Color.Parse("#8899aa"), palette["PlusTextPrimary"]);
            // accent 透明档由主色推导（alpha 收缩，RGB 保留）
            var muted = Assert.IsType<Color>(palette["PlusAccentMuted"]);
            Assert.Equal(0x55, muted.R);
            Assert.Equal(0x66, muted.G);
            Assert.Equal(0x77, muted.B);
            Assert.True(muted.A < 0x55);
        }
    }
}
