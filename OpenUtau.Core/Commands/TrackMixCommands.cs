using System;
using System.Threading.Tasks;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Vst;
using Serilog;

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

    /// <summary>
    /// Track-scoped convenience helpers.
    /// VST 槽位命令同时驱动实例生命周期（do/undo 触发 LoadEffectAsync/UnloadEffect），
    /// 并靠 VstSlotChangedNotification 让 UI 在异步加载完成后重建行。
    /// </summary>
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

        /// <summary>添加槽位并加载插件（do：写 UID + 异步 Load；undo：Unload + Clear）。</summary>
        public static UCommand AddVstSlot(UTrack track, int slotIndex, string pluginUid) {
            return new LambdaCommand(
                () => {
                    while (track.VstSlots.Count <= slotIndex)
                        track.VstSlots.Add(new VstPluginSlot(track.VstSlots.Count));
                    var slot = track.VstSlots[slotIndex];
                    slot.PluginUid = pluginUid;
                    if (!string.IsNullOrEmpty(pluginUid)) LoadAndNotify(track, slot);
                },
                () => {
                    if (slotIndex >= track.VstSlots.Count) return;
                    VstPluginManager.Inst.UnloadEffect(track.TrackNo, slotIndex);
                    track.VstSlots[slotIndex].Clear();
                },
                $"VST slot {track.TrackName}[{slotIndex}] ← {pluginUid}");
        }

        /// <summary>
        /// 移除槽位（do：Unload（内部 SaveState 写回 slot）+ Clear；undo：恢复 UID，
        /// 重新 Load 时 LoadAt 自动 RestoreState 还原参数）。
        /// </summary>
        public static UCommand RemoveVstSlot(UTrack track, int slotIndex) {
            string? oldUid = slotIndex < track.VstSlots.Count ? track.VstSlots[slotIndex].PluginUid : null;
            return new LambdaCommand(
                () => {
                    if (slotIndex >= track.VstSlots.Count) return;
                    VstPluginManager.Inst.UnloadEffect(track.TrackNo, slotIndex);
                    track.VstSlots[slotIndex].Clear();
                },
                () => {
                    while (track.VstSlots.Count <= slotIndex)
                        track.VstSlots.Add(new VstPluginSlot(track.VstSlots.Count));
                    if (slotIndex >= track.VstSlots.Count) return;
                    var slot = track.VstSlots[slotIndex];
                    slot.PluginUid = oldUid ?? "";
                    if (!string.IsNullOrEmpty(slot.PluginUid)) {
                        LoadAndNotify(track, slot);
                    }
                },
                $"Remove VST slot {track.TrackName}[{slotIndex}]");
        }

        /// <summary>
        /// 替换槽位插件（do：首次执行捕获旧 UID → 写新 UID + Load；undo：恢复旧 UID + Load）。
        /// 旧实例由 LoadAtAsync 内部替换并延迟销毁。
        /// </summary>
        public static UCommand SetVstPlugin(UTrack track, int slotIndex, string newUid) {
            return new SetVstPluginCommand(track, slotIndex, newUid);
        }

        sealed class SetVstPluginCommand : UCommand {
            readonly UTrack track;
            readonly int slotIndex;
            readonly string newUid;
            string oldUid = "";
            bool captured;

            public SetVstPluginCommand(UTrack track, int slotIndex, string newUid) {
                this.track = track;
                this.slotIndex = slotIndex;
                this.newUid = newUid;
            }

            public override void Execute() {
                if (!captured) {
                    // do 首次执行时捕获旧 UID（此时是真正替换前的值）
                    oldUid = slotIndex < track.VstSlots.Count ? track.VstSlots[slotIndex].PluginUid : "";
                    captured = true;
                }
                SetAndLoad(track, slotIndex, newUid);
            }

            public override void Unexecute() {
                SetAndLoad(track, slotIndex, oldUid);
            }

            public override string ToString() => $"VST slot {track.TrackName}[{slotIndex}] → {newUid}";
        }

        /// <summary>槽位旁路（数据级，下次渲染快照生效——无需重载实例）。</summary>
        public static UCommand ToggleVstBypass(UTrack track, int slotIndex) {
            if (slotIndex >= track.VstSlots.Count) return new LambdaCommand(() => { }, () => { }, "noop");
            var slot = track.VstSlots[slotIndex];
            bool old = slot.Bypassed;
            return new LambdaCommand(
                () => slot.Bypassed = !old,
                () => slot.Bypassed = old,
                $"Toggle bypass {track.TrackName}[{slotIndex}]");
        }

        // ── helpers ───────────────────────────────────────────────

        static void SetAndLoad(UTrack track, int slotIndex, string newUid) {
            while (track.VstSlots.Count <= slotIndex)
                track.VstSlots.Add(new VstPluginSlot(track.VstSlots.Count));
            if (slotIndex >= track.VstSlots.Count) return;
            var slot = track.VstSlots[slotIndex];
            slot.PluginUid = newUid;
            if (!string.IsNullOrEmpty(newUid)) {
                LoadAndNotify(track, slot);
            } else {
                VstPluginManager.Inst.UnloadEffect(track.TrackNo, slotIndex);
            }
        }

        /// <summary>
        /// 异步加载实例（原生秒级，移出 UI 线程）；完成后发 VstSlotChangedNotification
        /// （DocManager.ExecuteCmd 非 UI 线程自动回投）。undo 恢复旧 UID 时由 do 闭包
        /// 首次执行的旧值记录——SetVstPlugin 的 undo 需要旧值，见闭包设计。
        /// </summary>
        static void LoadAndNotify(UTrack track, VstPluginSlot slot) {
            _ = Task.Run(async () => {
                try {
                    await VstPluginManager.Inst.LoadEffectAsync(track.TrackNo, slot);
                    DocManager.Inst.ExecuteCmd(new VstSlotChangedNotification(track.TrackNo, slot.SlotIndex));
                } catch (Exception ex) {
                    Log.Error(ex, $"[VstCmd] Load failed T{track.TrackNo}S{slot.SlotIndex}");
                }
            });
        }
    }
}
