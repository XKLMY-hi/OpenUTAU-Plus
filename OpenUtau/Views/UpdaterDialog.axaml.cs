using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using OpenUtau.App.ViewModels;
using Serilog;
using OpenUtau.App.Controls;

namespace OpenUtau.App.Views {
    public partial class UpdaterDialog : UserControl {
        public readonly UpdaterViewModel ViewModel;
        public UpdaterDialog() {
            InitializeComponent();
            DataContext = ViewModel = new UpdaterViewModel();
        }

        /// <summary>Called by the host window when the overlay closes.</summary>
        public void OnClosed() {
            ViewModel.OnClosing();
        }

        /// <summary>
        /// Called at startup to check for updates silently.
        /// Only shows the dialog if an update is available.
        /// </summary>
        public static void CheckForUpdate(Action<UpdaterDialog> showDialog, Action closeApplication, TaskScheduler scheduler) {
            Task.Run(async () => {
                return await UpdaterViewModel.CheckForUpdateAsync();
            }).ContinueWith(t => {
                if (t.IsCompletedSuccessfully && t.Result) {
                    var dialog = new UpdaterDialog();
                    dialog.ViewModel.CloseApplication = closeApplication;
                    showDialog.Invoke(dialog);
                }
                if (t.IsFaulted) {
                    Log.Error(t.Exception, "[Updater] Failed to check for update");
                }
            }, scheduler).ContinueWith((t2, _) => {
                if (t2.IsFaulted) {
                    Log.Error(t2.Exception, "[Updater] Failed to show update dialog");
                }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
