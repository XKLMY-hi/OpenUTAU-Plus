using System.Windows.Input;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    public class MixerTrackStripViewModel : ViewModelBase {
        private readonly UTrack _track;
        public int TrackNo => _track.TrackNo;
        public string TrackName => _track.TrackName;
        public IBrush TrackColor => ThemeManager.GetTrackColor(_track.TrackColor).AccentColor;
        [Reactive] public double Volume { get; set; }
        [Reactive] public double Pan { get; set; }
        [Reactive] public bool Mute { get; set; }
        [Reactive] public bool Solo { get; set; }
        public ICommand ToggleMuteCmd { get; }
        public ICommand ToggleSoloCmd { get; }

        public MixerTrackStripViewModel(UTrack track) {
            _track = track;
            Volume = track.Volume;
            Pan = track.Pan * 100.0;
            Mute = track.Mute;
            Solo = track.Solo;
            ToggleMuteCmd = ReactiveCommand.Create(() => {
                Mute = !Mute;
                _track.Mute = Mute; _track.Muted = Mute;
                var vn = new VolumeChangeNotification(TrackNo, Mute ? -24 : Volume);
                DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(TrackMixCommands.Mute(_track, Mute));
                DocManager.Inst.EndUndoGroup();
                DocManager.Inst.ExecuteCmd(vn);
                MessageBus.Current.SendMessage(vn);
                MessageBus.Current.SendMessage(new TracksMuteEvent(TrackNo, false));
            });
            ToggleSoloCmd = ReactiveCommand.Create(() => {
                Solo = !Solo;
                _track.Solo = Solo;
                DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(TrackMixCommands.Solo(_track, Solo));
                DocManager.Inst.EndUndoGroup();
                MessageBus.Current.SendMessage(new TracksSoloEvent(TrackNo, Solo, false));
            });
        }

        public void ApplyVolume(double db) {
            db = System.Math.Clamp(db, -24, 12);
            Volume = db;
            _track.Volume = db;
        }

        public void ApplyPan(double value) {
            var clamped = System.Math.Clamp(value, -100, 100);
            Pan = clamped;
            _track.Pan = clamped / 100.0;
        }

        public void Refresh() {
            Volume = _track.Volume;
            Pan = _track.Pan * 100.0;
            Mute = _track.Mute;
            Solo = _track.Solo;
            this.RaisePropertyChanged(nameof(TrackName));
            this.RaisePropertyChanged(nameof(TrackColor));
        }

        public bool IsSilent => Mute || Volume <= -24;
    }
}
