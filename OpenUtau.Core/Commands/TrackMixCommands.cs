using System;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core {
    /// <summary>Generic reversible command backed by Action delegates.</summary>
    public class LambdaCommand : UCommand {
        readonly Action _do, _undo; readonly string _desc;
        public LambdaCommand(Action doAction, Action undoAction, string desc) {
            _do = doAction; _undo = undoAction; _desc = desc; }
        public override void Execute() => _do();
        public override void Unexecute() => _undo();
        public override string ToString() => _desc;
    }

    /// <summary>Track-scoped convenience helpers.</summary>
    public static class TrackMixCommands {
        public static UCommand Mute(UTrack track, bool newValue) {
            bool old = track.Mute;
            return new LambdaCommand(
                () => { track.Mute = newValue; track.Muted = newValue; },
                () => { track.Mute = old; track.Muted = old; },
                $"Mute track {track.TrackName} = {newValue}");
        }
        public static UCommand Solo(UTrack track, bool newValue) {
            bool old = track.Solo;
            return new LambdaCommand(
                () => track.Solo = newValue,
                () => track.Solo = old,
                $"Solo track {track.TrackName} = {newValue}");
        }
        public static UCommand AddVstSlot(UTrack track, int slotIndex, string pluginUid) {
            return new LambdaCommand(
                () => {
                    while (track.VstSlots.Count <= slotIndex)
                        track.VstSlots.Add(new Vst.VstPluginSlot(track.VstSlots.Count));
                    track.VstSlots[slotIndex].PluginUid = pluginUid;
                },
                () => { if (slotIndex < track.VstSlots.Count) track.VstSlots[slotIndex].Clear(); },
                $"VST slot {track.TrackName}[{slotIndex}] ← {pluginUid}");
        }
        public static UCommand RemoveVstSlot(UTrack track, int slotIndex) {
            string? oldUid = slotIndex < track.VstSlots.Count ? track.VstSlots[slotIndex].PluginUid : null;
            return new LambdaCommand(
                () => { if (slotIndex < track.VstSlots.Count) track.VstSlots[slotIndex].Clear(); },
                () => {
                    while (track.VstSlots.Count <= slotIndex)
                        track.VstSlots.Add(new Vst.VstPluginSlot(track.VstSlots.Count));
                    if (slotIndex < track.VstSlots.Count) track.VstSlots[slotIndex].PluginUid = oldUid ?? "";
                },
                $"Remove VST slot {track.TrackName}[{slotIndex}]");
        }
    }
}
