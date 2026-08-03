using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using OpenUtau.App.Controls;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Test.TestSupport;
using ReactiveUI;
using Xunit;

namespace OpenUtau.Test.App {
    public class MixerTrackStripTest {
        public MixerTrackStripTest() {
            // MixerTrackStrip.OnPanSliderValueChanged calls DocManager.Inst.ExecuteCmd;
            // wire it to run inline on the test (UI) thread.
            DocManagerTestSetup.RunOnCurrentThread();
        }
        /// <summary>
        /// Regression: LoadTrackData re-subscribed PanSlider.PropertyChanged each call
        /// without unsubscribing, so N refreshes caused one Value change to fire N
        /// PanChangeNotification messages. The fix (-= before +=) keeps it at one.
        /// </summary>
        [AvaloniaFact]
        public void RepeatedRefresh_StillSendsSinglePanNotification() {
            var track = new UTrack { TrackNo = 0 };
            var strip = new MixerTrackStrip(track);

            int received = 0;
            using var sub = MessageBus.Current.Listen<PanChangeNotification>()
                .ObserveOn(ImmediateScheduler.Instance)
                .Subscribe(_ => received++);

            // LoadTrackData is called once by the ctor; refresh 3 more times.
            strip.Refresh();
            strip.Refresh();
            strip.Refresh();

            // Changing the knob value should fire exactly one notification.
            strip.PanKnobControl.Value = 50;
            Assert.Equal(1, received);
        }

        /// <summary>
        /// DisposeSubscriptions must detach the ValueChanged handler so no further
        /// notifications fire after a strip is torn down.
        /// </summary>
        [AvaloniaFact]
        public void DisposeSubscriptions_StopsFurtherNotifications() {
            var track = new UTrack { TrackNo = 1 };
            var strip = new MixerTrackStrip(track);

            int received = 0;
            using var sub = MessageBus.Current.Listen<PanChangeNotification>()
                .ObserveOn(ImmediateScheduler.Instance)
                .Subscribe(_ => received++);

            strip.PanKnobControl.Value = 30;
            int beforeDispose = received;
            Assert.Equal(1, beforeDispose);

            strip.DisposeSubscriptions();
            strip.PanKnobControl.Value = 70;  // should produce no new notification
            Assert.Equal(beforeDispose, received);
        }
    }
}
