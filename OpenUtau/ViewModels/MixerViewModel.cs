using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    public class MixerViewModel : ViewModelBase, ICmdSubscriber {
        public ObservableCollection<UTrack> Tracks { get; } = new();

        [Reactive] public double MasterVolume { get; set; } = 0;
        [Reactive] public bool HasProject { get; set; }

        public ReactiveCommand<UTrack, Unit> OpenMixFxCommand { get; }

        public MixerViewModel() {
            OpenMixFxCommand = ReactiveCommand.Create<UTrack>(track => {
                if (track == null) return;
                if (Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) {
                    var existing = desktop.Windows.OfType<Views.MixFxDialog>().FirstOrDefault(w =>
                        w.DataContext is MixFxViewModel mvm && mvm.TrackName == track.TrackName);
                    if (existing != null) {
                        existing.Activate();
                        return;
                    }
                }
                var dialog = new Views.MixFxDialog(track);
                dialog.Show();
            });

            if (DocManager.Inst.Project?.tracks != null) {
                RefreshTracks();
            }
            DocManager.Inst.AddSubscriber(this);
        }

        public void OnNext(UCommand cmd, bool isUndo) {
            if (cmd is LoadProjectNotification || cmd is WillRemoveTrackNotification) {
                RefreshTracks();
            }
        }

        public void RefreshTracks() {
            Tracks.Clear();
            var project = DocManager.Inst.Project;
            if (project?.tracks == null) {
                HasProject = false;
                return;
            }
            foreach (var t in project.tracks) {
                Tracks.Add(t);
            }
            HasProject = Tracks.Count > 0;
        }

        public static string FormatVolume(double db) {
            if (db <= -24) return "-∞ dB";
            return $"{db:+0.0;-0.0} dB";
        }
    }
}
