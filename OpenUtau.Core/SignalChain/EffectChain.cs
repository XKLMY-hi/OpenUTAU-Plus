using System;
using OpenUtau.Core.SignalChain.Effects;
using Serilog;

namespace OpenUtau.Core.SignalChain {
    /// <summary>
    /// Per-track audio effect chain.  Renders the inner source into a scratch buffer,
    /// then applies a list of IEffect processors in series before additively mixing
    /// into the output.
    ///
    /// Generalised replacement for the old hardcoded FX chain (EQ → Comp → Reverb).
    /// Accepts any IEffect[] so VST plugins can be inserted alongside built-in FX.
    /// </summary>
    public class EffectChain : ISignalSource {
        public const int SampleRate = 44100;
        public const int Channels = 2;

        private readonly ISignalSource source;
        private readonly IEffect[] effects;
        private float[]? scratch;

        public EffectChain(ISignalSource source, IEffect[] effects) {
            this.source = source;
            this.effects = effects;
            // 预分配典型块大小（VST Setup 块 4096），避免播放中按需扩容
            scratch = new float[4096];
        }

        public bool IsReady(int position, int count) => source.IsReady(position, count);

        /// <summary>Total reported latency of all effects in the chain (for future PDC).</summary>
        public int TotalLatency {
            get {
                int total = 0;
                foreach (var fx in effects) total += fx.LatencySamples;
                return total;
            }
        }

        /// <summary>Drop time-domain state of all effects. Call on seek/position jump.</summary>
        public void Reset() {
            foreach (var fx in effects)
                fx.Reset();
        }

        public int Mix(int position, float[] buffer, int index, int count) {
            if (scratch == null || scratch.Length < count)
                scratch = new float[count];
            Array.Clear(scratch, 0, count);
            int ret = source.Mix(position, scratch, 0, count);

            foreach (var fx in effects) {
                if (!fx.IsBypassed)
                    fx.Process(scratch, 0, count);
            }

            for (int i = 0; i < count; i++)
                buffer[index + i] += scratch[i];

            return ret;
        }

        /// <summary>True when any effect in the chain is active.</summary>
        public bool HasActiveEffects {
            get {
                foreach (var fx in effects)
                    if (!fx.IsBypassed) return true;
                return false;
            }
        }

        // ── Factory helpers ───────────────────────────────────────

        /// <summary>
        /// Build an EffectChain from existing UMixFx (backward compatible).
        /// Returns the inner source unchanged when no FX are enabled.
        /// </summary>
        public static ISignalSource Build(ISignalSource inner, Core.Ustx.UMixFx? fx, IEffect[]? extraEffects = null) {
            var list = new System.Collections.Generic.List<IEffect>();

            if (fx != null && fx.Enabled) {
                Log.Information($"[EffectChain] Building chain: Eq={!fx.EqBypassed} Comp={!fx.CompBypassed} Rev={!fx.ReverbBypassed}");
                if (!fx.EqBypassed) {
                    var eq = new BiquadEQ(SampleRate, Channels);
                    eq.Configure(fx.EqLowDb, fx.EqMidFreq, 0.707, fx.EqMidDb, fx.EqHighDb);
                    Log.Information($"[EffectChain] EQ bypassed={eq.IsBypassed} low={fx.EqLowDb} midF={fx.EqMidFreq} mid={fx.EqMidDb} high={fx.EqHighDb}");
                    if (!eq.IsBypassed) list.Add(eq);
                }
                if (!fx.CompBypassed) {
                    var comp = new SimpleCompressor(SampleRate, Channels);
                    FxPresets.CompParams cParams = FxPresets.Comp.TryGetValue(fx.CompPreset ?? FxPresets.Off, out var cp)
                        ? cp : FxPresets.Comp[FxPresets.Off];
                    comp.Configure(fx.CompThresholdDb, fx.CompRatio, cParams.AttackMs, cParams.ReleaseMs, fx.CompMakeupDb);
                    if (!comp.IsBypassed) list.Add(comp);
                }
                if (!fx.ReverbBypassed) {
                    var reverb = new Freeverb(SampleRate, Channels);
                    FxPresets.ReverbParams rParams = FxPresets.Reverb.TryGetValue(fx.ReverbPreset ?? FxPresets.Off, out var rp)
                        ? rp : FxPresets.Reverb[FxPresets.Off];
                    double userWet = Math.Clamp(fx.ReverbWet, 0.0, 2.0);
                    reverb.Configure(fx.ReverbSize, fx.ReverbDamp, rParams.Width,
                                     rParams.Wet * userWet, rParams.Dry, fx.ReverbPreDelayMs);
                    if (!reverb.IsBypassed) list.Add(reverb);
                }
            }

            if (extraEffects != null) {
                foreach (var e in extraEffects)
                    list.Add(e);
            }

            if (list.Count == 0) {
                Log.Information("[EffectChain] No active effects — returning inner source unchanged");
                return inner;
            }
            Log.Information($"[EffectChain] Created with {list.Count} effect(s)");
            return new EffectChain(inner, list.ToArray());
        }
    }
}
