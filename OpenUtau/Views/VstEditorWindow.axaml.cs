using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenUtau.App.Controls;
using OpenUtau.App.ViewModels;
using OpenUtau.Core.Vst;
using ReactiveUI;

namespace OpenUtau.App.Views {
    public partial class VstEditorWindow : WindowEx {
        private readonly VstEditorViewModel? _vm;

        public VstEditorWindow() {
            InitializeComponent();
            _vm = new VstEditorViewModel();
            DataContext = _vm;
            ApplyBindings();
            NativeStatus.Text = ThemeManager.GetString("vsteditor.noplugin");
        }

        public VstEditorWindow(VstEffect fx) {
            InitializeComponent();
            _vm = new VstEditorViewModel(fx);
            DataContext = _vm;
            ApplyBindings();
            Title = _vm.DisplayName;
            OpenEditorBtn.Content = ThemeManager.GetString("vsteditor.opengui");
            OpenEditorBtn.IsEnabled = true;
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        void ApplyBindings() {
            if (_vm == null) return;
            TitleLabel.Text = _vm.DisplayName;
            TypeLabel.Text = _vm.TypeLabel;
            TypeBadge.Background = _vm.TypeBadgeColor;
            VendorLabel.Text = _vm.Vendor;
            SubsLabel.Text = _vm.SubCategories;
            PathLabel.Text = _vm.PathDisplay;
            ParamInfo.Text = _vm.ParamInfo;
            _vm.WhenAnyValue(x => x.StatusText).Subscribe(s => NativeStatus.Text = s);
        }

        public void OnOpenEditorClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) {
            _vm?.OpenEditorCommand.Execute(null);
        }
        public void OnCloseClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) {
            Close();
        }
    }
}
