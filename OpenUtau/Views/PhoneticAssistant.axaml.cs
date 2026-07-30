using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.App.Controls;

namespace OpenUtau.App.Views {
    public partial class PhoneticAssistant : WindowEx {
        PhoneticAssistantViewModel viewModel;
        public PhoneticAssistant() {
            InitializeComponent();
            DataContext = viewModel = new PhoneticAssistantViewModel();
        }

        public void OnCopy(object sender, RoutedEventArgs e) {
            Clipboard?.SetTextAsync(viewModel.Phonemes);
        }
    }
}
