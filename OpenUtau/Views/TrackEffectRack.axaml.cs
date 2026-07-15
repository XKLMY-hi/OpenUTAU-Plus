using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.SignalChain.Effects;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Vst;
using OpenUtau.Core.Util;
using ReactiveUI;

namespace OpenUtau.App.Views {
    public partial class TrackEffectRack : Window {
        private readonly UTrack track;
        private UMixFx fx => track.MixFx ??= new();

        private readonly List<Preferences.MixFxUserPreset> userPresets = new();
        private readonly Preferences.MixFxUserPreset defaultPreset;

        public TrackEffectRack() : this(new UTrack()) { }

        public TrackEffectRack(UTrack track) {
            InitializeComponent();
            this.track = track;
            TitleLabel.Text = $"{track.TrackName}";

            // Init presets =================================================
            defaultPreset = new Preferences.MixFxUserPreset {
                Name = ThemeManager.GetString("mixfx.library.default"),
                Fx = RecommendedFx(),
            };
            userPresets.Add(defaultPreset);
            foreach (var p in Preferences.Default.MixFxUserPresets ?? new())
                userPresets.Add(p);
            RefreshPresetCombo();

            if (track.VstSlots == null || track.VstSlots.Count == 0)
                track.VstSlots = VstPluginManager.CreateDefaultSlots(3);

            // Sync UI state ================================================
            EnableToggle.IsChecked = fx.Enabled;
            ExportCheck.IsChecked = Preferences.Default.MixFxApplyOnExportMixdown;

            // Top row buttons ==============================================
            RecBtn.Click += (_, _) => LoadPreset(defaultPreset);
            SaveBtn.Click += (_, _) => SavePreset();
            DelBtn.Click += (_, _) => DeletePreset();
            PresetCombo.SelectionChanged += (_, _) => {
                if (PresetCombo.SelectedItem is Preferences.MixFxUserPreset p) LoadPreset(p);
            };
            EnableToggle.Tapped += (_, _) => {
                fx.Enabled = !fx.Enabled;
                EnableToggle.IsChecked = fx.Enabled;
                NotifyChanged(); BuildUI();
            };

            BuildUI();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Presets
        // ═══════════════════════════════════════════════════════════════════

        private static UMixFx RecommendedFx() => new() {
            Enabled = true,
            EqPreset = "vocal_air", CompPreset = "gentle", ReverbPreset = "small_room",
            EqLowDb = 2, EqMidFreq = 3000, EqMidDb = 1.5, EqHighDb = 3,
            CompThresholdDb = -18, CompRatio = 2, CompMakeupDb = 2.5,
            ReverbSize = 0.3, ReverbDamp = 0.7, ReverbWet = 1, ReverbPreDelayMs = 0,
        };

        private void RefreshPresetCombo() {
            PresetCombo.ItemsSource = null;
            PresetCombo.ItemsSource = userPresets.ToList();
            PresetCombo.SelectedItem = defaultPreset;
        }

        private void LoadPreset(Preferences.MixFxUserPreset p) {
            if (p.Fx == null) return;
            var f = p.Fx;
            fx.Enabled = f.Enabled; fx.EqPreset = f.EqPreset; fx.CompPreset = f.CompPreset; fx.ReverbPreset = f.ReverbPreset;
            fx.EqBypassed = f.EqBypassed; fx.CompBypassed = f.CompBypassed; fx.ReverbBypassed = f.ReverbBypassed;
            fx.EqLowDb = f.EqLowDb; fx.EqMidFreq = f.EqMidFreq; fx.EqMidDb = f.EqMidDb; fx.EqHighDb = f.EqHighDb;
            fx.CompThresholdDb = f.CompThresholdDb; fx.CompRatio = f.CompRatio; fx.CompMakeupDb = f.CompMakeupDb;
            fx.ReverbSize = f.ReverbSize; fx.ReverbDamp = f.ReverbDamp; fx.ReverbWet = f.ReverbWet; fx.ReverbPreDelayMs = f.ReverbPreDelayMs;
            EnableToggle.IsChecked = fx.Enabled;
            NotifyChanged(); BuildUI();
        }

        private async void SavePreset() {
            var dlg = new TypeInDialog { Title = ThemeManager.GetString("mixfx.library.save") };
            dlg.SetText("");
            string? name = null;
            dlg.onFinish = n => { if (!string.IsNullOrWhiteSpace(n)) name = n; };
            await dlg.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(name) || name == defaultPreset.Name) return;

            var snap = new Preferences.MixFxUserPreset { Name = name, Fx = Snap() };
            userPresets.RemoveAll(x => x.Name == name && x != defaultPreset);
            userPresets.Add(snap);
            SavePresets();
            RefreshPresetCombo();
            PresetCombo.SelectedItem = snap;
        }

        private void DeletePreset() {
            if (PresetCombo.SelectedItem is not Preferences.MixFxUserPreset p || p == defaultPreset) return;
            userPresets.Remove(p);
            SavePresets();
            RefreshPresetCombo();
            PresetCombo.SelectedItem = defaultPreset;
        }

        private void SavePresets() {
            Preferences.Default.MixFxUserPresets = userPresets.Where(x => x != defaultPreset).ToList();
            Preferences.Save();
        }

        private UMixFx Snap() => new() {
            Enabled = fx.Enabled,
            EqPreset = fx.EqPreset, CompPreset = fx.CompPreset, ReverbPreset = fx.ReverbPreset,
            EqBypassed = fx.EqBypassed, CompBypassed = fx.CompBypassed, ReverbBypassed = fx.ReverbBypassed,
            EqLowDb = fx.EqLowDb, EqMidFreq = fx.EqMidFreq, EqMidDb = fx.EqMidDb, EqHighDb = fx.EqHighDb,
            CompThresholdDb = fx.CompThresholdDb, CompRatio = fx.CompRatio, CompMakeupDb = fx.CompMakeupDb,
            ReverbSize = fx.ReverbSize, ReverbDamp = fx.ReverbDamp, ReverbWet = fx.ReverbWet, ReverbPreDelayMs = fx.ReverbPreDelayMs,
        };

        // ═══════════════════════════════════════════════════════════════════
        //  UI Builder
        // ═══════════════════════════════════════════════════════════════════

        private void BuildUI() {
            SlotList.Children.Clear();

            // ── Built-in FX (compact rows) ───────────────────────────────
            BuildBuiltInRow(0, "EQ", fx.EqBypassed,
                v => { fx.EqBypassed = v; NotifyChanged(); BuildUI(); },
                () => fx.EqPreset, v => { fx.EqPreset = v; LoadEqPreset(v); NotifyChanged(); },
                FxPresets.EqPresetNames,
                new[] { Param("Low", -12, 12, fx.EqLowDb, v => { fx.EqLowDb = v; }, "F1"),
                        Param("Mid Freq", 200, 6000, fx.EqMidFreq, v => { fx.EqMidFreq = v; }, "F0"),
                        Param("Mid", -12, 12, fx.EqMidDb, v => { fx.EqMidDb = v; }, "F1"),
                        Param("High", -12, 12, fx.EqHighDb, v => { fx.EqHighDb = v; }, "F1") });

            BuildBuiltInRow(1, "Compressor", fx.CompBypassed,
                v => { fx.CompBypassed = v; NotifyChanged(); BuildUI(); },
                () => fx.CompPreset, v => { fx.CompPreset = v; LoadCompPreset(v); NotifyChanged(); },
                FxPresets.CompPresetNames,
                new[] { Param("Thresh", -40, 0, fx.CompThresholdDb, v => { fx.CompThresholdDb = v; }, "F1"),
                        Param("Ratio", 1, 20, fx.CompRatio, v => { fx.CompRatio = v; }, "F1"),
                        Param("Makeup", 0, 12, fx.CompMakeupDb, v => { fx.CompMakeupDb = v; }, "F1") });

            BuildBuiltInRow(2, "Reverb", fx.ReverbBypassed,
                v => { fx.ReverbBypassed = v; NotifyChanged(); BuildUI(); },
                () => fx.ReverbPreset, v => { fx.ReverbPreset = v; LoadReverbPreset(v); NotifyChanged(); },
                FxPresets.ReverbPresetNames,
                new[] { Param("Size", 0, 1, fx.ReverbSize, v => { fx.ReverbSize = v; }, "F2"),
                        Param("Damp", 0, 1, fx.ReverbDamp, v => { fx.ReverbDamp = v; }, "F2"),
                        Param("Wet", 0, 2, fx.ReverbWet, v => { fx.ReverbWet = v; }, "F2"),
                        Param("Pre-Delay", 0, 200, fx.ReverbPreDelayMs, v => { fx.ReverbPreDelayMs = v; }, "F0") });

            // ── VST section ──────────────────────────────────────────────
            SlotList.Children.Add(new TextBlock { Classes = { "section" }, Text = "VST Plugins" });

            foreach (var slot in track.VstSlots)
                BuildVstRow(slot);

            if (track.VstSlots.Count < 8) {
                var add = new Button { Classes = { "addBtn" }, Content = "+ Add VST slot", Margin = new(0, 4, 0, 0) };
                add.Click += (_, _) => { track.VstSlots.Add(new(track.VstSlots.Count)); BuildUI(); };
                SlotList.Children.Add(add);
            }
        }

        // ── Compact built-in row ──────────────────────────────────────

        private void BuildBuiltInRow(int idx, string name, bool bypassed,
            Action<bool> setBypassed, Func<string> getPreset, Action<string> setPreset,
            IReadOnlyList<string> presets, Control[] params_) {

            // Header row
            var headerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            headerRow.Children.Add(new TextBlock {
                Text = $"{idx + 1}. {name}", FontSize = 12, FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var toggle = new ToggleSwitch { IsChecked = !bypassed, OnContent = "On", OffContent = "Off", FontSize = 10 };
            toggle.Tapped += (_, _) => {
                setBypassed(!bypassed);
                toggle.IsChecked = !bypassed;
                NotifyChanged(); BuildUI();
            };
            headerRow.Children.Add(toggle); Grid.SetColumn(toggle, 1);

            // Preset combo
            var combo = new ComboBox { ItemsSource = presets, SelectedItem = getPreset(),
                Margin = new(0, 2, 0, 4), IsEnabled = !bypassed };
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string s) setPreset(s); };

            // Param grid (hidden when bypassed) — each row is its own 3-col grid
            var paramStack = new StackPanel { Spacing = 1 };
            foreach (var c in params_)
                paramStack.Children.Add(c);

            var contentStack = new StackPanel { Spacing = 3, Margin = new(0, 4, 0, 2),
                IsVisible = !bypassed };
            contentStack.Children.Add(combo);
            contentStack.Children.Add(paramStack);

            var outerStack = new StackPanel();
            outerStack.Children.Add(headerRow);
            outerStack.Children.Add(contentStack);

            var row = new Border { Classes = { "slotRow" }, Child = outerStack };
            if (bypassed) row.Classes.Add("bypassed");
            SlotList.Children.Add(row);
        }

        // ── Slider helper ───────────────────────────────────────────────

        private static Grid Param(string label, double min, double max, double val,
            Action<double> onChange, string fmt) {
            var g = new Grid { Margin = new(0, 1),
                ColumnDefinitions = new ColumnDefinitions("70,*,45") };
            g.Children.Add(new TextBlock { Text = label, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 });
            var s = new Slider { Classes = { "param" }, Minimum = min, Maximum = max, Value = val,
                TickFrequency = (max - min) / 100 };
            var vl = new TextBlock { Text = val.ToString(fmt), FontSize = 9, FontFamily = "monospace",
                VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Opacity = 0.7 };
            s.PropertyChanged += (_, e) => {
                if (e.Property == RangeBase.ValueProperty) { onChange(s.Value); vl.Text = s.Value.ToString(fmt); }
            };
            g.Children.Add(s); Grid.SetColumn(s, 1);
            g.Children.Add(vl); Grid.SetColumn(vl, 2);
            return g;
        }

        // ── VST row ─────────────────────────────────────────────────────

        private void BuildVstRow(VstPluginSlot slot) {
            var row = new Border { Classes = { "slotRow" } };
            var g = new Grid { ColumnDefinitions = new("Auto,*,Auto,Auto") };

            // Number + type badge
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftStack.Children.Add(new TextBlock {
                Text = $"{slot.SlotIndex + 4}", FontSize = 11, Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center, FontFamily = "monospace", Margin = new(0, 0, 6, 0),
            });
            if (slot.IsLoaded) {
                leftStack.Children.Add(new TextBlock {
                    Classes = { "typeLabel" }, Text = "[VST]",
                });
                leftStack.Children.Add(new TextBlock {
                    Classes = { "slotName" }, Text = slot.DisplayName,
                });
            } else {
                leftStack.Children.Add(new TextBlock {
                    Classes = { "empty" }, Text = "Empty slot",
                });
            }
            g.Children.Add(leftStack);
            Grid.SetColumn(leftStack, 1);

            // Bypass toggle (always show for loaded slots)
            if (slot.IsLoaded) {
                var bt = new ToggleSwitch {
                    IsChecked = !slot.Bypassed, OnContent = "On", OffContent = "Off",
                    FontSize = 10, Margin = new(6, 0, 0, 0),
                };
                bt.Tapped += (_, _) => {
                    slot.Bypassed = !slot.Bypassed;
                    bt.IsChecked = !slot.Bypassed;
                    BuildUI();
                };
                g.Children.Add(bt); Grid.SetColumn(bt, 2);

                var rm = new Button { Classes = { "removeBtn" } };
                var s = slot; rm.Click += (_, _) => { s.Clear(); BuildUI(); };
                g.Children.Add(rm); Grid.SetColumn(rm, 3);
            } else {
                var browse = new Button { Classes = { "browseBtn" }, HorizontalAlignment = HorizontalAlignment.Right };
                var s = slot; browse.Click += (_, _) => BrowsePlugin(s);
                g.Children.Add(browse); Grid.SetColumn(browse, 3);
            }

            row.Child = g;
            if (slot.IsLoaded && slot.Bypassed) row.Classes.Add("bypassed");
            SlotList.Children.Add(row);
        }

        private void BrowsePlugin(VstPluginSlot slot) {
            VstPluginRegistry.Inst.ScanAll();
            // Only show EFFECT plugins — instruments would crash the chain
            var allPlugins = VstPluginRegistry.Inst.Effects;
            var instruments = VstPluginRegistry.Inst.All.Where(p => !p.IsEffect).ToList();
            if (allPlugins.Count == 0) {
                string msgText = "No VST/VST3 effect plugins found.";
                if (instruments.Count > 0)
                    msgText += $"\n\n{instruments.Count} instrument plugin(s) were excluded" +
                               " (instruments cannot be used as effects).";
                msgText += "\n\nAdd scan paths in Settings → OpenUTAU Plus.";
                var msg = new Window { Title = "No plugins", Width = 380, Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner };
                var sp = new StackPanel { Margin = new(14) };
                sp.Children.Add(new TextBlock { Text = msgText, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
                var ok = new Button { Content = "OK", Width = 60, HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new(0, 8, 0, 0) };
                ok.Click += (_, _) => msg.Close(); sp.Children.Add(ok);
                msg.Content = new Border { Child = sp };
                msg.ShowDialog(this);
                return;
            }

            var picker = new Window { Title = "Select Effect Plugin", Width = 500, Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background };
            var layout = new StackPanel { Margin = new(12) };
            var infoRow = new TextBlock {
                Text = $"Only audio effects shown ({allPlugins.Count} found" +
                       (instruments.Count > 0 ? $", {instruments.Count} instruments excluded)" : ")"),
                FontSize = 11, Margin = new(0, 0, 0, 4), Opacity = 0.6,
            };
            layout.Children.Add(infoRow);

            // Search box
            var search = new TextBox {
                Watermark = "Filter plugins...", FontSize = 11,
                Margin = new(0, 0, 0, 6),
            };
            layout.Children.Add(search);

            var lb = new ListBox { ItemsSource = allPlugins.ToList(), Height = 240 };
            layout.Children.Add(lb);

            // Search filter
            search.TextChanged += (_, _) => {
                var filter = search.Text?.ToLowerInvariant() ?? "";
                lb.ItemsSource = string.IsNullOrEmpty(filter)
                    ? allPlugins
                    : allPlugins.Where(p => p.Name.ToLowerInvariant().Contains(filter)).ToList();
            };

            var btns = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 8, 0, 0), Spacing = 8 };
            var load = new Button { Content = "Load", Width = 64 };
            var cancel = new Button { Content = "Cancel", Width = 64 };
            btns.Children.Add(load); btns.Children.Add(cancel);
            layout.Children.Add(btns);
            picker.Content = new Border { Child = layout };
            load.Click += (_, _) => {
                if (lb.SelectedItem is VstPluginEntry e) { slot.PluginUid = e.Uid; BuildUI(); }
                picker.Close();
            };
            cancel.Click += (_, _) => picker.Close();
            picker.ShowDialog(this);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Preset loaders
        // ═══════════════════════════════════════════════════════════════════

        void LoadEqPreset(string k) { if (FxPresets.Eq.TryGetValue(k, out var p)) { fx.EqLowDb = p.LowDb; fx.EqMidFreq = p.MidFreq; fx.EqMidDb = p.MidDb; fx.EqHighDb = p.HighDb; } }
        void LoadCompPreset(string k) { if (FxPresets.Comp.TryGetValue(k, out var p)) { fx.CompThresholdDb = p.ThresholdDb; fx.CompRatio = p.Ratio; fx.CompMakeupDb = p.MakeupDb; } }
        void LoadReverbPreset(string k) { if (FxPresets.Reverb.TryGetValue(k, out var p)) { fx.ReverbSize = p.RoomSize; fx.ReverbDamp = p.Damp; fx.ReverbWet = 1.0; fx.ReverbPreDelayMs = p.PreDelayMs; } }

        void NotifyChanged() => MessageBus.Current.SendMessage(new MixFxChangedNotification(track.TrackNo));

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            Preferences.Default.MixFxApplyOnExportMixdown = ExportCheck.IsChecked == true;
            Preferences.Save();
        }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.Key == Key.Escape) Close(); }
    }
}
