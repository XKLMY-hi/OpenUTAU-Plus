using System;
using System.Diagnostics;
using System.Threading;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// TrackMixCommands 的 VST 槽位命令 do/undo 往返——数据层（slot）与实例层
    /// （VstPluginManager 异步加载）双重断言。FakeVstBridge 注入 + 轮询等待异步 Load。
    /// </summary>
    public class TrackMixCommandsTest {
        const string UidA = "test:cmd-a";
        const string UidB = "test:cmd-b";

        public TrackMixCommandsTest() {
            VstTestSetup.Register(VstTestSetup.MakeEntry(UidA, "CmdA"));
            VstTestSetup.Register(VstTestSetup.MakeEntry(UidB, "CmdB"));
        }

        static UTrack MakeTrack() {
            var track = new UTrack();
            track.VstSlots = VstPluginManager.CreateDefaultSlots(3);
            return track;
        }

        /// <summary>轮询等待条件成立（异步 Load 完成后断言）。</summary>
        static void WaitFor(Func<bool> cond, int timeoutMs = 3000) {
            var sw = Stopwatch.StartNew();
            while (!cond()) {
                Assert.True(sw.ElapsedMilliseconds < timeoutMs, "WaitFor timed out");
                Thread.Sleep(10);
            }
        }

        [Fact]
        public void AddVstSlot_DoUndo_RoundTrip() {
            var bridge = new FakeVstBridge();
            VstPluginManager.Inst.Bridge = bridge;
            try {
                var track = MakeTrack();
                var cmd = TrackMixCommands.AddVstSlot(track, 3, UidA);

                cmd.Execute();
                Assert.Equal(UidA, track.VstSlots[3].PluginUid);
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 3) != null);

                cmd.Unexecute();
                Assert.Equal("", track.VstSlots[3].PluginUid);
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 3) == null);
            } finally {
                VstPluginManager.Inst.ClearAll();
            }
        }

        [Fact]
        public void RemoveVstSlot_Undo_RestoresUidAndInstance() {
            var bridge = new FakeVstBridge();
            VstPluginManager.Inst.Bridge = bridge;
            try {
                var track = MakeTrack();
                // 先加载一个插件到槽 0
                var loadCmd = TrackMixCommands.AddVstSlot(track, 0, UidA);
                loadCmd.Execute();
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 0) != null);

                var cmd = TrackMixCommands.RemoveVstSlot(track, 0);
                cmd.Execute();
                Assert.Equal("", track.VstSlots[0].PluginUid);
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 0) == null);

                // undo：恢复 UID + 重载实例
                cmd.Unexecute();
                Assert.Equal(UidA, track.VstSlots[0].PluginUid);
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 0) != null);
            } finally {
                VstPluginManager.Inst.ClearAll();
            }
        }

        [Fact]
        public void SetVstPlugin_DoUndo_RestoresOldUid() {
            var bridge = new FakeVstBridge();
            VstPluginManager.Inst.Bridge = bridge;
            try {
                var track = MakeTrack();
                // 槽 0 初始为 UidA
                TrackMixCommands.AddVstSlot(track, 0, UidA).Execute();
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 0) != null);

                var cmd = TrackMixCommands.SetVstPlugin(track, 0, UidB);
                cmd.Execute();
                Assert.Equal(UidB, track.VstSlots[0].PluginUid);
                // 旧实例进延迟销毁；新实例加载
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 0)?.Slot?.PluginUid == UidB);

                cmd.Unexecute();
                Assert.Equal(UidA, track.VstSlots[0].PluginUid);
                WaitFor(() => VstPluginManager.Inst.GetEffect(track.TrackNo, 0)?.Slot?.PluginUid == UidA);
            } finally {
                VstPluginManager.Inst.ClearAll();
            }
        }

        [Fact]
        public void ToggleVstBypass_DoUndo_RoundTrip() {
            var track = MakeTrack();
            var slot = track.VstSlots[0];
            Assert.False(slot.Bypassed);

            var cmd = TrackMixCommands.ToggleVstBypass(track, 0);
            cmd.Execute();
            Assert.True(slot.Bypassed);

            cmd.Unexecute();
            Assert.False(slot.Bypassed);
        }
    }
}
