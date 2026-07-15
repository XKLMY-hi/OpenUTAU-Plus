using System.Collections.Concurrent;
using OpenUtau.Core.SignalChain;

namespace OpenUtau.Core.Render {
    /// <summary>
    /// Thread-safe registry of per-track and master LevelTracker instances.
    /// Populated by RenderEngine during render setup, read by mixer UI via timer.
    /// </summary>
    public static class TrackLevels {
        private static readonly ConcurrentDictionary<int, LevelTracker> _trackers = new();
        private static LevelTracker? _masterTracker;

        public static void Register(int trackNo, LevelTracker tracker) {
            _trackers[trackNo] = tracker;
        }

        public static void RegisterMaster(LevelTracker tracker) {
            _masterTracker = tracker;
        }

        /// <summary>Read current peak dB for a track (-60..0), then reset.</summary>
        public static float ReadAndReset(int trackNo) {
            if (_trackers.TryGetValue(trackNo, out var t))
                return t.ReadAndResetPeakDb();
            return -60f;
        }

        /// <summary>Read master bus peak dB (-60..0), then reset.</summary>
        public static float ReadMasterAndReset() {
            return _masterTracker?.ReadAndResetPeakDb() ?? -60f;
        }

        public static void Clear() {
            _trackers.Clear();
            _masterTracker = null;
        }
    }
}
