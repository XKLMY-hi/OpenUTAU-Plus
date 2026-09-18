using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Audio;
using OpenUtau.Core;
using OpenUtau.Core.Export;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test.App {
    /// <summary>
    /// 合成层 / 音频消费层解耦契约（"合成分管线"接缝）。
    ///
    /// 约定：播放与导出层只通过 <see cref="RenderEngine"/> 的静态门面使用合成能力，
    /// 不得构造 RenderEngine、不得引用其内部状态类型（PhraseRenderCache）、不得知道
    /// 两批播放策略与乐句缓存的存在。这样将来把合成实现整根换成上游实现（frozen
    /// slot planner + 优先级调度）时，运输/混音/导出层零改动。
    ///
    /// 本测试即为该约定的**可执行证明**：任何"把合成内部状态重新漏进播放/导出层"
    /// 的改动都会在这里变红。
    /// </summary>
    public class AudioSeamContractTests {
        // ── 1. 合成层静态门面必须齐备且签名稳定 ─────────────────────────────
        [Theory]
        [InlineData("PreRender")]
        [InlineData("RenderProject")]
        [InlineData("RenderMixdown")]
        [InlineData("RenderTracks")]
        [InlineData("InvalidatePhraseCache")]
        [InlineData("ReleaseSourceTemp")]
        public void RenderEngine_ExposesStaticFacade(string name) {
            var method = typeof(RenderEngine).GetMethod(
                name, BindingFlags.Public | BindingFlags.Static);
            Assert.True(method != null, $"RenderEngine.{name} 必须是 public static（合成层对外门面）");
        }

        [Fact]
        public void RenderEngine_FacadeSignatures_AreStable() {
            // 播放：返回 MasterAdapter（消费层唯一需要的合成产物形态）
            var play = typeof(RenderEngine).GetMethod("RenderProject", BindingFlags.Public | BindingFlags.Static);
            Assert.Equal(typeof(Tuple<MasterAdapter, System.Collections.Generic.List<Fader>>),
                play!.ReturnType);

            // 离线混音：返回 WaveMix
            var mixdown = typeof(RenderEngine).GetMethod("RenderMixdown", BindingFlags.Public | BindingFlags.Static);
            Assert.Equal(typeof(Tuple<WaveMix, System.Collections.Generic.List<Fader>>),
                mixdown!.ReturnType);

            // 逐轨：WaveMix 列表
            var tracks = typeof(RenderEngine).GetMethod("RenderTracks", BindingFlags.Public | BindingFlags.Static);
            Assert.Equal(typeof(System.Collections.Generic.List<WaveMix>), tracks!.ReturnType);
        }

        // ── 2. 播放层不得知道合成内部状态 ──────────────────────────────────
        [Fact]
        public void PlaybackManager_DoesNotOwnSynthesisCache() {
            var type = typeof(PlaybackManager);
            var offending = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType.Name.Contains("PhraseCache") || f.FieldType.Name.Contains("PhraseRenderCache"))
                .Select(f => f.Name)
                .ToArray();
            Assert.True(offending.Length == 0,
                $"PlaybackManager 不得持有合成层缓存字段，发现：{string.Join(", ", offending)}");

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.Name.Contains("PhraseCache") || p.PropertyType.Name.Contains("PhraseRenderCache"))
                .Select(p => p.Name)
                .ToArray();
            Assert.True(props.Length == 0,
                $"PlaybackManager 不得暴露合成层缓存属性，发现：{string.Join(", ", props)}");
        }

        // ── 3. 导出层不得知道合成内部状态（构造签名即接口） ────────────────
        [Fact]
        public void ExportSession_CtorTakesNoSynthesisCache() {
            var ctors = typeof(ExportSession).GetConstructors();
            Assert.Single(ctors);
            var ps = ctors[0].GetParameters();
            Assert.Equal(3, ps.Length);
            Assert.DoesNotContain(ps, p => p.ParameterType.Name.Contains("PhraseCache"));
        }

        [Fact]
        public void ExportSession_Options_HasNoCacheKnob() {
            var names = typeof(ExportSession.Options).GetFields().Select(f => f.Name).ToArray();
            Assert.DoesNotContain("Cache", names);
            Assert.DoesNotContain("PhraseCache", names);
        }

        // ── 4. 消费段计数属于音频层，且两层都能用 ────────────────────────
        [Fact]
        public void RenderGate_LivesInAudioLayer_AndCountsInFlight() {
            Assert.Equal("OpenUtau.Audio", typeof(RenderGate).Namespace);

            int baseline = RenderGate.InFlight;
            using (RenderGate.Enter()) {
                Assert.Equal(baseline + 1, RenderGate.InFlight);
            }
            Assert.Equal(baseline, RenderGate.InFlight);
        }

        // ── 5. 缓存整体失效经过门面（播放层唯一允许的接触方式）────────────
        [Fact]
        public void InvalidatePhraseCache_IsCallable_WithoutTouchingInternals() {
            RenderEngine.InvalidatePhraseCache(); // 不得抛（幂等）
        }

        // ── 6. 移植探针：合成实现可被替换的最小证明 ───────────────────────
        /// <summary>
        /// 合成层与运输层之间只应有「音频数据」（ISignalSource / WaveMix）与「混音与
        /// 输出形态」（Fader / MasterAdapter）两类类型跨越。若将来合成实现整根替换
        /// （上游 frozen slot planner），运输层依赖的类型集合不应扩大。
        ///
        /// 本测试用否定清单而非许可清单：合成**内部实现类型**一旦出现在运输层的
        /// 字段/属性签名里即失败。这些类型就是"换合成后端时会连坐运输层"的载体。
        /// </summary>
        [Fact]
        public void TransportLayer_DoesNotLeakSynthesisImplementationTypes() {
            string[] synthesisInternals = {
                "RenderEngine", "RenderPhrase", "RenderNote", "RenderPhone",
                "RenderPartRequest", "WaveSource", "PhraseRenderCache",
                "RenderResult", "RenderPitchResult",
            };
            var asm = typeof(PlaybackManager).Assembly;
            var playback = asm.GetType("OpenUtau.Core.PlaybackManager")!;
            var leaked = playback
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.FieldType.Name)
                .Concat(playback.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.PropertyType.Name))
                .Where(synthesisInternals.Contains)
                .Distinct()
                .ToArray();

            Assert.True(leaked.Length == 0,
                $"运输层字段/属性泄漏了合成内部类型（换合成后端会连坐）：{string.Join(", ", leaked)}");
        }

        /// <summary>
        /// 显式确认"唯一入口"：PlaybackManager 与 ExportSession 的公开成员里不得出现
        /// RenderEngine 实例（静态门面调用不算——它不进签名）。
        /// </summary>
        [Fact]
        public void FacadeIsTheOnlyEntry_NoRenderEngineInPublicSurface() {
            foreach (var t in new[] { typeof(PlaybackManager), typeof(ExportSession) }) {
                var bad = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.ReturnType == typeof(RenderEngine) ||
                                m.GetParameters().Any(p => p.ParameterType == typeof(RenderEngine)))
                    .Select(m => m.Name)
                    .ToArray();
                Assert.True(bad.Length == 0, $"{t.Name} 公开成员出现 RenderEngine：{string.Join(", ", bad)}");
            }
        }
    }
}
