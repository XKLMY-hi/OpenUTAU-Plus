using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using OpenUtau.Core.Format;
using Serilog;

namespace OpenUtau.Core {
    public class SineGenerator : ISampleProvider {
        public WaveFormat WaveFormat => waveFormat;
        private WaveFormat waveFormat;

        private readonly double attackSampleCount;
        private readonly double releaseSampleCount;

        public double freq { get; set; }

        private int position;
        private int releasePosition = 0;
        private float gain = 1;

        public bool isActive { get; private set; } = true;
        public bool isPlaying { get; private set; } = true;

        public SineGenerator(double freq, float gain, int attackMs = 25, int releaseMs = 25) {
            waveFormat = SignalChain.AudioSettings.CreateIeeeFloatWaveFormat();
            this.freq = freq;
            this.gain = gain;
            position = 0;

            // Number of samples the attack & release fades take
            attackSampleCount = (attackMs / 1000.0f) * waveFormat.SampleRate;
            releaseSampleCount = (releaseMs / 1000.0f) * waveFormat.SampleRate;
        }

        public int Read(float[] buffer, int offset, int count) {
            // Duplicate sample across two channels
            for (int i = 0; i < count / 2; i++) {
                float sample = GetNextSample();
                buffer[offset + (i * 2)] += (float)sample * gain;
                buffer[offset + (i * 2) + 1] += (float)sample * gain;
            }
            return count;
        }

        private float GetNextSample() {
            double delta = 2 * Math.PI * freq / waveFormat.SampleRate;
            double sample = Math.Sin(position * delta);

            // Calculate attack envelope
            sample *= Math.Clamp(position / attackSampleCount, 0, 1);

            // Calculate release envelope
            double releaseEnvelope = 1;
            if (!isActive) {
                releaseEnvelope = Math.Clamp(1.0f - ((position - releasePosition) / releaseSampleCount), 0, 1);
            }
            sample *= releaseEnvelope;

            if (releaseEnvelope < double.Epsilon) {
                // Stop sampling this generator if release is completed
                // Instance will be cleaned up later
                isPlaying = false;
            }

            position++;
            return (float)sample * gain;
        }

        public void Stop() {
            if (!isActive) return;

            isActive = false;
            releasePosition = position;
        }
    }

    public class ToneGenerator : ISignalSource {
        private Dictionary<double, SineGenerator> activeFrequencies = new Dictionary<double, SineGenerator>();
        private List<SineGenerator> inactiveFrequencies = new List<SineGenerator>();
        private readonly float gain = 0.4f;

        private readonly object _lockObj = new object();

        public ToneGenerator() {}

        public ToneGenerator(float gain) {
            this.gain = gain;
        }

        public bool IsReady(int position, int count) {
            return true;
        }

        public int Mix(int position, float[] buffer, int offset, int count) {
            lock (_lockObj) {
                foreach (var freqEntry in activeFrequencies) {
                    if (freqEntry.Value.isPlaying) {
                        freqEntry.Value.Read(buffer, offset, count);
                    }
                }
                foreach (var generator in inactiveFrequencies) {
                    if (generator.isPlaying) {
                        generator.Read(buffer, offset, count);
                    }
                }
            }

            return position + count;
        }
        public void StartTone(double freq) {
            if (activeFrequencies.ContainsKey(freq)) {
                if (activeFrequencies[freq].isActive) {
                    // Don't cut off tone to replace with the same frequency
                    // Should never happen
                    return;
                }
            }

            lock (_lockObj) {
                activeFrequencies[freq] = new SineGenerator(freq, gain);
            }
        }

        public void EndTone(double freq) {
            if (activeFrequencies.ContainsKey(freq)) {
                activeFrequencies[freq].Stop();

                lock (_lockObj) {
                    // Move to inactive frequencies list
                    inactiveFrequencies.Add(activeFrequencies[freq]);
                    activeFrequencies.Remove(freq);
                }
            }

            CleanupTones();
        }

        public void EndAllTones() {
            foreach (var tone in activeFrequencies) {
                tone.Value.Stop();

                lock (_lockObj) {
                    // Move to inactive frequencies list
                    inactiveFrequencies.Add(tone.Value);
                    activeFrequencies.Remove(tone.Key);
                }
            }


            CleanupTones();
        }

        private void CleanupTones() {
            lock (_lockObj) {
                inactiveFrequencies.RemoveAll(gen => !gen.isPlaying);
            }
        }
    }

    public class PlaybackManager : SingletonBase<PlaybackManager>, ICmdSubscriber {
        private PlaybackManager() {
            DocManager.Inst.AddSubscriber(this);
            try {
                Directory.CreateDirectory(PathManager.Inst.CachePath);
                RenderEngine.ReleaseSourceTemp();
            } catch (Exception e) {
                Log.Error(e, "Failed to release source temp.");
            }

            toneGenerator = new ToneGenerator();
            editingMix = new MasterAdapter(toneGenerator);
        }

        public readonly ToneGenerator toneGenerator;
        /// <summary>内存短语渲染缓存（seek/循环复用，LRU 封顶，LoadProject 清空）。</summary>
        public readonly Render.PhraseRenderCache PhraseCache = new();
        List<Fader> faders;
        MasterAdapter masterMix;
        MasterAdapter editingMix;
        
        double startMs;
        public int StartTick => DocManager.Inst.Project.timeAxis.MsPosToTickPos(startMs);
        CancellationTokenSource renderCancellation;

        // Loop playback state
        private int loopStartTick = 0;
        private int loopEndTick = -1;

        public Audio.IAudioOutput AudioOutput { get; set; } = new Audio.DummyAudioOutput();
        public bool OutputActive => AudioOutput.PlaybackState == PlaybackState.Playing;
        public bool StartingToPlay { get; private set; }
        public bool PlayingMaster { get; private set; }

        // 当前 Init 的 provider 包装层——Stop 后靠它确认音频回调线程已退出在飞 Read（B1 竞态屏障）
        private Audio.CallbackTrackedSampleProvider? trackedProvider;

        /// <summary>统一换源入口：包装 provider 供 drain 追踪。</summary>
        private void InitOutput(ISampleProvider provider) {
            var tracked = new Audio.CallbackTrackedSampleProvider(provider);
            trackedProvider = tracked;
            AudioOutput.Init(tracked);
        }

        /// <summary>
        /// 阻塞直到音频回调线程退出在飞 Read（须在 AudioOutput.Stop() 之后调用）。
        /// Stop 后设备不再拉取新数据，在飞的 Read 会在毫秒级完成退出；
        /// 超时保护（回调卡死时不阻塞 UI），超时记日志继续。
        /// </summary>
        private void WaitForCallbackDrain() {
            var provider = trackedProvider;
            if (provider == null) return;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var spin = new System.Threading.SpinWait();
            while (provider.InCallback) {
                if (sw.ElapsedMilliseconds > 2000) {
                    Log.Warning("WaitForCallbackDrain timed out — audio callback stuck in Read.");
                    return;
                }
                spin.SpinOnce();
            }
        }

        // 混音台主推子（masterMix 创建前暂存，StartPlayback 时应用）
        private double masterVolumeDb = 0;
        public bool MasterMuted { get; private set; }

        public void ApplyMasterVolume(double db) {
            masterVolumeDb = db;
            if (masterMix != null) {
                masterMix.Scale = MasterMuted ? 0 : DecibelToVolume(db);
            }
        }

        public void SetMasterMuted(bool muted) {
            MasterMuted = muted;
            if (masterMix != null) {
                masterMix.Scale = muted ? 0 : DecibelToVolume(masterVolumeDb);
            }
        }

        /// <summary>主输出峰值 dB（-60..0，主推子后实际输出），混音台主电平表用。</summary>
        public float ReadMasterLevelDb() {
            return masterMix?.ReadAndResetPeakDb() ?? -60f;
        }

        public void PlayTestSound() {
            masterMix = null;
            PlayingMaster = false;
            AudioOutput.Stop();
            InitOutput(new SignalGenerator(SignalChain.AudioSettings.SampleRate, 1).Take(TimeSpan.FromSeconds(1)));
            AudioOutput.Play();
        }

        public void PlayTone(double freq) {
            toneGenerator.StartTone(freq);

            // If nothing is playing, start editing mix
            if (!OutputActive) {
                AudioOutput.Stop();
                InitOutput(editingMix);
                AudioOutput.Play();
            }
        }

        public void EndTone(double freq) {
            toneGenerator.EndTone(freq);
        }

        public void EndAllTones() {
            toneGenerator.EndAllTones();
        }

        public void PlayFile(string file) {
            masterMix = null;
            if (AudioOutput.PlaybackState == PlaybackState.Playing) {
                AudioOutput.Stop();
            }
            try{
                var playSound = Wave.OpenFile(file);
                InitOutput(playSound.ToSampleProvider());
            } catch (Exception ex) {
                Log.Error(ex, $"Failed to load sample {file}.");
                return;
            }
            AudioOutput.Play();
        }

        // ── 独立试听通道（NAudio WaveOutEvent，不影响主 AudioOutput/工程播放）──

        private WaveOutEvent? previewOutput;
        private string? previewPath;
        /// <summary>当前试听文件（null = 无试听）。</summary>
        public string? PreviewPath => previewPath;
        /// <summary>试听状态变化（开始/停止/播完）——参数为当前试听文件或 null。</summary>
        public event Action<string?>? PreviewChanged;

        public void PlayPreview(string file) {
            StopPreview();
            try {
                var playSound = Wave.OpenFile(file);
                var output = new WaveOutEvent();
                output.Init(playSound);
                output.PlaybackStopped += (_, _) => {
                    if (ReferenceEquals(previewOutput, output)) {
                        previewOutput = null;
                        previewPath = null;
                        PreviewChanged?.Invoke(null);
                    }
                    output.Dispose();
                };
                previewOutput = output;
                previewPath = file;
                output.Play();
                PreviewChanged?.Invoke(previewPath);
            } catch (Exception ex) {
                Log.Error(ex, $"Failed to play preview {file}.");
                StopPreview();
            }
        }

        public void StopPreview() {
            if (previewOutput is { } output) {
                previewOutput = null;
                previewPath = null;
                output.Stop();
                output.Dispose();
                PreviewChanged?.Invoke(null);
            }
        }

        public void PlayOrPause(int tick = -1, int endTick = -1, int trackNo = -1) {
            if (PlayingMaster) {
                PausePlayback();
            } else {
                int rangeStart = DocManager.Inst.rangeStartTick;
                int rangeEnd = DocManager.Inst.rangeEndTick;
                if (rangeEnd > rangeStart) {
                    int playPos = DocManager.Inst.playPosTick;
                    loopStartTick = rangeStart;
                    loopEndTick = rangeEnd;
                    Play(
                        DocManager.Inst.Project,
                        tick: tick == -1 ? ((playPos >= rangeStart && playPos < rangeEnd) ? playPos : rangeStart) : tick,
                        endTick: endTick == -1 ? rangeEnd : endTick,
                        trackNo: trackNo);
                } else {
                    loopEndTick = -1;
                    Play(
                        DocManager.Inst.Project,
                        tick: tick == -1 ? DocManager.Inst.playPosTick : tick,
                        endTick: endTick,
                        trackNo: trackNo);
                }
            }
        }

        public void Play(UProject project, int tick, int endTick = -1, int trackNo = -1) {
            if (AudioOutput.PlaybackState == PlaybackState.Paused) {
                PlayingMaster = true;
                AudioOutput.Play();
                return;
            }
            AudioOutput.Stop();
            Render(project, tick, endTick, trackNo);
            StartingToPlay = true;
            PlayingMaster = true;
        }

        public void StopPlayback() {
            AudioOutput.Stop();
            // 旧回调线程退出后才允许释放其引用的资源（VST 延迟销毁的 Flush 安全点之一）
            WaitForCallbackDrain();
            PlayingMaster = false;
            loopEndTick = -1;
            TrackLevels.Clear();
            // 失效旧渲染的 faders 引用（OnNext 已有 null 检查——seek/停止后音量/声像通知不再写入过期链）
            faders = null;
            // 安全点：输出已停 + 回调已 drain，可释放延迟销毁的 VST handle
            Vst.VstPluginManager.Inst.TryFlushAllPendingDispose();
        }

        public void PausePlayback() {
            AudioOutput.Pause();
            PlayingMaster = false;
            loopEndTick = -1;
        }

        private void StartPlayback(double startMs, MasterAdapter masterAdapter) {
            toneGenerator.EndAllTones();

            this.startMs = startMs;
            var start = TimeSpan.FromMilliseconds(startMs);
            Log.Information($"StartPlayback at {start}");
            masterMix = masterAdapter;
            masterMix.Scale = MasterMuted ? 0 : DecibelToVolume(masterVolumeDb);
            AudioOutput.Stop();
            // 换源前确认旧回调线程已退出（B1 竞态屏障——旧链上的 VST handle 可安全延迟销毁）
            WaitForCallbackDrain();
            // 安全点：换源完成、新链 Init 前，释放旧链延迟销毁的 VST handle
            Vst.VstPluginManager.Inst.TryFlushAllPendingDispose();
            InitOutput(masterMix);
            AudioOutput.Play();
        }

        private void Render(UProject project, int tick, int endTick, int trackNo) {
            Task.Run(() => {
                try {
                    RenderEngine engine = new RenderEngine(project, startTick: tick, endTick: endTick, trackNo: trackNo, cache: PhraseCache);
                    var result = engine.RenderProject(DocManager.Inst.MainScheduler, ref renderCancellation);
                    faders = result.Item2;
                    StartingToPlay = false;
                    StartPlayback(project.timeAxis.TickPosToMsPos(tick), result.Item1);
                } catch (Exception e) {
                    Log.Error(e, "Failed to render.");
                    StopPlayback();
                    var customEx = new MessageCustomizableException("Failed to render.", "<translate:errors.failed.render>", e);
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(customEx));
                }
            });
        }

        public void UpdatePlayPos() {
            if (AudioOutput != null && AudioOutput.PlaybackState == PlaybackState.Playing && PlayingMaster) {
                // 硬件位置（字节）→ float 采样数 → 扣除等待静音（帧数）→ ms（格式显式化）
                double ms = (AudioOutput.GetPosition() / sizeof(float) - masterMix.Waited / masterMix.WaveFormat.Channels)
                    * 1000.0 / masterMix.WaveFormat.SampleRate;
                int tick = DocManager.Inst.Project.timeAxis.MsPosToTickPos(startMs + ms);
                if (loopEndTick > 0 && tick >= loopEndTick) {
                    // Loop back to range start
                    Play(DocManager.Inst.Project, tick: loopStartTick, endTick: loopEndTick);
                    return;
                }
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick, masterMix.IsWaiting));
            } else if (AudioOutput != null && AudioOutput.PlaybackState == PlaybackState.Stopped && PlayingMaster) {
                // Playback stopped (e.g. silence at end). Check if we should loop.
                if (loopEndTick > 0) {
                    Play(DocManager.Inst.Project, tick: loopStartTick, endTick: loopEndTick);
                    return;
                }
            }
        }

        public static float DecibelToVolume(double db) {
            return (db <= -24) ? 0 : (float)MusicMath.DecibelToLinear((db < -16) ? db * 2 + 16 : db);
        }

        // Exporting mixdown
        public async Task RenderMixdown(UProject project, string exportPath) {
            await Task.Run(() => {
                try {
                    RenderEngine engine = new RenderEngine(project);
                    var projectMix = engine.RenderMixdown(DocManager.Inst.MainScheduler, ref renderCancellation, wait: true).Item1;
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Exporting to {exportPath}."));

                    CheckFileWritable(exportPath);
                    // 导出消费段进入在飞计数——防止并发 Flush 释放正在被消费的 VST handle
                    using (Vst.RenderGate.Enter()) {
                        WaveFileWriter.CreateWaveFile16(exportPath, new ExportAdapter(projectMix));
                    }
                    // 安全点：导出消费段已退出
                    Vst.VstPluginManager.Inst.TryFlushAllPendingDispose();
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Exported to {exportPath}."));
                } catch (IOException ioe) {
                    var customEx = new MessageCustomizableException($"Failed to export {exportPath}.", $"<translate:errors.failed.export>: {exportPath}", ioe);
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(customEx));
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Failed to export {exportPath}."));
                } catch (Exception e) {
                    var customEx = new MessageCustomizableException("Failed to render.", $"<translate:errors.failed.render>: {exportPath}", e);
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(customEx));
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Failed to render."));
                }
            });
        }

        // Exporting each tracks
        public async Task RenderToFiles(UProject project, string exportPath) {
            await Task.Run(() => {
                string file = "";
                try {
                    RenderEngine engine = new RenderEngine(project);
                    var trackMixes = engine.RenderTracks(DocManager.Inst.MainScheduler, ref renderCancellation);
                    // 分轨写文件循环：消费段在飞计数（防并发 Flush）
                    using (Vst.RenderGate.Enter()) {
                        for (int i = 0; i < trackMixes.Count; ++i) {
                            if (trackMixes[i] == null || i >= project.tracks.Count || project.tracks[i].Muted) {
                                continue;
                            }
                            file = PathManager.Inst.GetExportPath(exportPath, project.tracks[i]);
                            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Exporting to {file}."));

                            CheckFileWritable(file);
                            WaveFileWriter.CreateWaveFile16(file, new ExportAdapter(trackMixes[i]).ToMono(1, 0));
                            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Exported to {file}."));
                        }
                    }
                    // 安全点：导出消费段已退出
                    Vst.VstPluginManager.Inst.TryFlushAllPendingDispose();
                } catch (IOException ioe) {
                    var customEx = new MessageCustomizableException($"Failed to export {file}.", $"<translate:errors.failed.export>: {file}", ioe);
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(customEx));
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Failed to export {file}."));
                } catch (Exception e) {
                    var customEx = new MessageCustomizableException("Failed to render.", "<translate:errors.failed.render>", e);
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(customEx));
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Failed to render."));
                }
            });
        }

        private void CheckFileWritable(string filePath) {
            if (!File.Exists(filePath)) {
                return;
            }
            using (FileStream fp = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) {
                return;
            }
        }

        void SchedulePreRender() {
            Log.Information("SchedulePreRender");
            var engine = new RenderEngine(DocManager.Inst.Project);
            engine.PreRenderProject(ref renderCancellation);
        }

        #region ICmdSubscriber

        public void OnNext(UCommand cmd, bool isUndo) {
            if (cmd is SeekPlayPosTickNotification) {
                var _cmd = cmd as SeekPlayPosTickNotification;
                StopPlayback();
                int tick = _cmd!.playPosTick;
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick, false, _cmd.pause));
            } else if (cmd is VolumeChangeNotification) {
                var _cmd = cmd as VolumeChangeNotification;
                if (faders != null && faders.Count > _cmd.TrackNo) {
                    faders[_cmd.TrackNo].Scale = DecibelToVolume(_cmd.Volume);
                }
            } else if (cmd is PanChangeNotification) {
                var _cmd = cmd as PanChangeNotification;
                if (faders != null && faders.Count > _cmd!.TrackNo) {
                    faders[_cmd.TrackNo].Pan = (float)_cmd.Pan;
                }
            } else if (cmd is MasterVolumeChangeNotification masterVol) {
                ApplyMasterVolume(masterVol.Volume);
            } else if (cmd is LoadProjectNotification) {
                StopPlayback();
                renderCancellation?.Cancel();
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(0));
            }
            if (cmd is PreRenderNotification || cmd is LoadProjectNotification) {
                if (Util.Preferences.Default.PreRender) {
                    SchedulePreRender();
                }
            }
        }

        #endregion
    }
}
