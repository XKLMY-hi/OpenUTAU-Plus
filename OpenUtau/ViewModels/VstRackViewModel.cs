using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Vst;
using ReactiveUI;

namespace OpenUtau.App.ViewModels {
    public class VstSlotItem {
        public VstPluginSlot Slot { get; }
        public int Index => Slot.SlotIndex;
        public bool IsLoaded => Slot.IsLoaded;
        public string DisplayName => Slot.DisplayName;
        public VstSlotItem(VstPluginSlot slot) => Slot = slot;
    }

    public class VstRackViewModel : ViewModelBase {
        private readonly UTrack _track;
        public string Title { get; }
        public ObservableCollection<VstSlotItem> Slots { get; } = new();
        public bool CanAddSlot => _track.VstSlots.Count < 8;
        public ICommand AddSlotCommand { get; }
        public ICommand RemoveSlotCommand { get; }
        public ICommand LoadPluginCommand { get; }

        public VstRackViewModel(UTrack track) {
            _track = track;
            Title = $"{track.TrackName} — VST Rack";
            if (track.VstSlots == null || track.VstSlots.Count == 0)
                track.VstSlots = VstPluginManager.CreateDefaultSlots(3);
            RefreshSlots();

            AddSlotCommand = ReactiveCommand.Create(() => {
                _track.VstSlots.Add(new VstPluginSlot(_track.VstSlots.Count));
                RefreshSlots();
                this.RaisePropertyChanged(nameof(CanAddSlot));
            });

            RemoveSlotCommand = ReactiveCommand.Create<VstPluginSlot>(slot => {
                slot.Clear();
                RefreshSlots();
            });

            LoadPluginCommand = ReactiveCommand.Create<VstPluginSlot>(slot => {
                // Plugin selection is delegated back to View (requires picker dialog)
            });
        }

        public void RefreshSlots() {
            Slots.Clear();
            foreach (var s in _track.VstSlots) Slots.Add(new VstSlotItem(s));
            this.RaisePropertyChanged(nameof(CanAddSlot));
        }

        public void LoadPlugin(VstPluginSlot slot, string pluginUid) {
            slot.PluginUid = pluginUid;
            VstPluginManager.Inst.LoadEffect(_track.TrackNo, slot);
            RefreshSlots();
        }

        public List<VstPluginInfo> GetAvailablePlugins() {
            VstPluginManager.Inst.ScanPlugins();
            return VstPluginManager.Inst.KnownPlugins.Values.OrderBy(p => p.PluginName).ToList();
        }
    }
}
