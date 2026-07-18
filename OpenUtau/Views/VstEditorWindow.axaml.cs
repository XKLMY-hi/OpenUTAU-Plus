using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using OpenUtau.Core.Util;
using OpenUtau.App.Controls;
using OpenUtau.Core.Vst;
using Serilog;

namespace OpenUtau.App.Views {
    public partial class VstEditorWindow : WindowEx {
        private readonly VstEffect _fx;

        public VstEditorWindow() {
            InitializeComponent();
            _fx = null!;
            NativeStatus.Text = ThemeManager.GetString("vsteditor.noplugin");
        }

        /// <summary>
        /// Open editor for an already-loaded VstEffect.
        /// The VstEffect instance is shared with RenderEngine —
        /// native GUI changes take effect on audio immediately.
        /// </summary>
        public VstEditorWindow(VstEffect fx) {
            InitializeComponent();
            _fx = fx;

            var entry = fx.Entry;
            Title = entry.Name;
            TitleLabel.Text = entry.Name;

            string typeText = entry.Type == VstPluginType.VST3
                ? (entry.IsEffect ? "VST3 Effect" : "VST3 Instrument")
                : (entry.IsEffect ? "VST2 Effect" : "VST2 Instrument");
            TypeLabel.Text = typeText;
            TypeBadge.Background = entry.IsEffect
                ? new SolidColorBrush(Color.FromRgb(60, 160, 100))
                : new SolidColorBrush(Color.FromRgb(220, 120, 40));

            VendorLabel.Text = !string.IsNullOrEmpty(entry.Vendor)
                ? entry.Vendor : ThemeManager.GetString("vsteditor.novendor");

            SubsLabel.Text = entry.SubCategories.Count > 0
                ? string.Join(" · ", entry.SubCategories) : ThemeManager.GetString("vsteditor.nocategory");

            string path = entry.Path;
            if (path.Length > 60) path = "..." + path[^57..];
            PathLabel.Text = path;

            ParamInfo.Text = $"{entry.CategoryDisplay}  ·  {entry.Vendor}";

            OpenEditorBtn.IsEnabled = true;
            OpenEditorBtn.Content = ThemeManager.GetString("vsteditor.opengui");

            KeyDown += (_, e) => {
                if (e.Key == Key.Escape) Close();
            };
        }

        public void OnOpenEditorClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) {
            if (_fx == null) return;

            try {
                bool ok = _fx.OpenNativeEditor();
                if (ok)
                    NativeStatus.Text = ThemeManager.GetString("vsteditor.nativeopened");
                else
                    NativeStatus.Text = $"{ThemeManager.GetString("vsteditor.noeditor")}: {VstBridge.LastError() ?? ThemeManager.GetString("vsteditor.nogui")}";
            } catch (Exception ex) {
                NativeStatus.Text = $"{ThemeManager.GetString("vsteditor.error")}: {ex.Message}";
                Log.Error(ex, $"[VstEditor] Failed for {_fx.DisplayName}");
            }
        }

        public void OnCloseClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) {
            Close();
        }
    }
}
