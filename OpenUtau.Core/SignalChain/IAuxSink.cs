namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// Interface for tracks that can send audio to an auxiliary bus (Aux send).
    /// Not yet implemented in the render engine — interface stub for README roadmap
    /// (远期愿景: 发送轨 / Aux 总线 / 侧链压缩).
    /// </summary>
    public interface IAuxSink {
        /// <summary>Route a copy of the post-fader signal to a destination bus at a given level.</summary>
        void SendTo(ISignalSource target, float level);
    }
}
