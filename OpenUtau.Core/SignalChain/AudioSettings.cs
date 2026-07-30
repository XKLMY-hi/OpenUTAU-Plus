namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// Centralised audio constants.  Previously 44100 and 2 were hardcoded in
    /// EffectChain, MasterAdapter, ExportAdapter, Fader, and others.
    /// </summary>
    public static class AudioSettings {
        public const int SampleRate = 44100;
        public const int Channels = 2;
    }
}
