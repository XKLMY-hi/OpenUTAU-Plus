using Avalonia.Controls;
using Avalonia.Input;
using OpenUtau.Core.Vst;

namespace OpenUtau.App.Views {
    public partial class VstEditorWindow : Window {
        public VstEditorWindow() : this("", "") { }
        public VstEditorWindow(string pluginName, string pluginUid) {
            InitializeComponent();
            Title = $"{pluginName}";
            TitleLabel.Text = pluginName;
            StatusText.Text = $"Native GUI coming in Phase 3\n{pluginUid}";
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) Close();
        }
    }
}
