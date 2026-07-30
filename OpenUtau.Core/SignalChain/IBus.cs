namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// An auxiliary bus that mixes multiple sources into a single output.
    /// Not yet wired into RenderEngine — interface stub for README roadmap.
    /// </summary>
    public interface IBus {
        /// <summary>Mixed output of all sources added to this bus.</summary>
        ISignalSource Output { get; }

        /// <summary>Add a source to the bus mix.</summary>
        void AddSource(ISignalSource src);
    }
}
