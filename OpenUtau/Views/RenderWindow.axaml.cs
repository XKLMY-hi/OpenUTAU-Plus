using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using OpenUtau.App.Controls;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using NAudio.Wave;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.App.Views {
    public partial class RenderWindow : WindowEx {
        private CancellationTokenSource? _cts;
        private readonly List<(CheckBox cb, Core.Ustx.UTrack track)> _trackChecks = new();

        public RenderWindow() {
            InitializeComponent();

            // Range radio — show/hide custom box
            RadioFullSong.IsCheckedChanged += (_, _) => CustomRangeBox.IsVisible = false;
            RadioLoop.IsCheckedChanged += (_, _) => CustomRangeBox.IsVisible = false;
            RadioCustom.IsCheckedChanged += (_, _) => CustomRangeBox.IsVisible = RadioCustom.IsChecked == true;

            // Default output path
            var proj = DocManager.Inst.Project;
            if (!string.IsNullOrEmpty(proj.FilePath)) {
                var dir = Path.GetDirectoryName(proj.FilePath) ?? "";
                var name = Path.GetFileNameWithoutExtension(proj.FilePath);
                OutputPathBox.Text = Path.Combine(dir, $"{name}.wav");
            }

            BuildTrackList();
        }

        // ═══════════════════════════════════════════════════════════════
        //  Track checkboxes
        // ═══════════════════════════════════════════════════════════════

        void BuildTrackList() {
            TrackCheckPanel.Children.Clear();
            _trackChecks.Clear();
            foreach (var t in DocManager.Inst.Project.tracks) {
                var row = new StackPanel {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2)
                };
                var cb = new CheckBox {
                    IsChecked = true,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var label = new TextBlock {
                    Text = t.TrackName,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0),
                };
                row.Children.Add(cb);
                row.Children.Add(label);
                TrackCheckPanel.Children.Add(row);
                _trackChecks.Add((cb, t));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Browse output path
        // ═══════════════════════════════════════════════════════════════

        public async void OnBrowsePath(object? sender, RoutedEventArgs args) {
            var file = await FilePicker.SaveFile(this, "menu.file.exportmixdown", FilePicker.WAV);
            if (!string.IsNullOrEmpty(file))
                OutputPathBox.Text = file;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Start render
        // ═══════════════════════════════════════════════════════════════

        public void OnStartRender(object? sender, RoutedEventArgs args) {
            string path = OutputPathBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(path)) {
                var proj = DocManager.Inst.Project;
                var dir = Path.GetDirectoryName(proj.FilePath)
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var name = Path.GetFileNameWithoutExtension(proj.FilePath) ?? "Untitled";
                path = Path.Combine(dir, $"{name}.wav");
                OutputPathBox.Text = path;
            }

            StartBtn.IsEnabled = false;
            ProgressLabel.Text = ThemeManager.GetString("render.status.preparing");
            ProgressSubLabel.IsVisible = true;
            ProgressBarControl.Value = 0;

            bool isMixdown = RadioMixdown.IsChecked == true;
            bool applyMixFx = (ChkVst.IsChecked == true) || (ChkBuiltinFx.IsChecked == true);
            var project = DocManager.Inst.Project;
            var selTrackNos = _trackChecks.Where(x => x.cb.IsChecked == true)
                .Select(x => x.track.TrackNo).ToHashSet();

            _cts = new CancellationTokenSource();
            var ctx = _cts;

            Task.Run(() => {
                try {
                    RenderEngine engine = new RenderEngine(project);

                    if (isMixdown) {
                        // ── 实时录制模式（静音播放，绝对精确含全部效果） ──
                        Dispatcher.UIThread.Invoke(() => {
                            ProgressLabel.Text = ThemeManager.GetString("render.status.mixdown");
                            ProgressBarControl.Value = 20;
                        });

                        var renderResult = engine.RenderProject(
                            DocManager.Inst.MainScheduler, ref ctx);
                        var masterAdapter = renderResult.Item1;

                        Dispatcher.UIThread.Invoke(() => {
                            ProgressLabel.Text = ThemeManager.GetString("render.status.writing");
                            ProgressBarControl.Value = 70;
                        });

                        using var writer = new WaveFileWriter(
                            File.Create(path), WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                        var recorder = new RecordingAdapter(masterAdapter, writer, silentOutput: true);

                        float[] buf = new float[4096];
                        // 导出消费段进入在飞计数（防并发 Flush 释放正在被消费的 VST handle）
                        using (OpenUtau.Core.Vst.RenderGate.Enter()) {
                            DrainExport(
                                () => recorder.Read(buf, 0, buf.Length),
                                totalWritten => {
                                    var sec = totalWritten / 44100.0 / 2.0;
                                    Dispatcher.UIThread.Invoke(() => {
                                        ProgressBarControl.Value = 70 + Math.Min(25, sec / 60.0 * 25);
                                    });
                                },
                                ctx.Token);
                        }
                    } else {
                        var trackMixes = engine.RenderTracks(
                            DocManager.Inst.MainScheduler, ref ctx);

                        int total = project.tracks.Count;
                        for (int i = 0; i < Math.Min(trackMixes.Count, total); i++) {
                            if (ctx.IsCancellationRequested) break;
                            if (trackMixes[i] == null || project.tracks[i].Muted) continue;
                            if (!selTrackNos.Contains(i)) continue;

                            var track = project.tracks[i];
                            int idx = i;
                            Dispatcher.UIThread.Invoke(() => {
                                ProgressLabel.Text = string.Format(ThemeManager.GetString("render.status.exporting"), idx + 1, total, track.TrackName);
                                ProgressBarControl.Value = 20 + (60 * idx / total);
                            });

                            string trackPath = Path.Combine(
                                Path.GetDirectoryName(path) ?? "",
                                $"{Path.GetFileNameWithoutExtension(path)}_{Sanitize(track.TrackName)}.wav");
                            // 导出消费段进入在飞计数（防并发 Flush）
                            using (OpenUtau.Core.Vst.RenderGate.Enter()) {
                                WriteWavFile(trackPath, trackMixes[i]);
                            }
                        }
                    }

                    Dispatcher.UIThread.Invoke(() => {
                        ProgressLabel.Text = ThemeManager.GetString("render.status.done");
                        ProgressSubLabel.IsVisible = false;
                        ProgressBarControl.Value = 100;
                    });
                } catch (Exception ex) {
                    Log.Error(ex, "[RenderWindow] Render failed");
                    Dispatcher.UIThread.Invoke(() => {
                        ProgressLabel.Text = ThemeManager.GetString("render.status.failed");
                        ProgressSubLabel.Text = ex.Message;
                    });
                } finally {
                    Dispatcher.UIThread.Invoke(() => StartBtn.IsEnabled = true);
                }
            }, _cts.Token);
        }

        // ═══════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════

        static void WriteWavFile(string path, ISignalSource source) {
            var adapter = new ExportAdapter(source);
            WaveFileWriter.CreateWaveFile16(path, adapter);
        }

        static string Sanitize(string name) {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "_");
            return name;
        }

        /// <summary>
        /// Drain a sample reader into a WAV file until EOF or cancellation.
        /// Extracted from OnStartRender for testability: the drain loop, cancellation
        /// check, and accumulated-sample progress tracking are pure logic with no UI deps.
        /// </summary>
        /// <param name="readChunk">Reads one chunk; returns sample count, 0 on EOF.</param>
        /// <param name="onProgress">Receives the running total of samples written.</param>
        /// <param name="ct">Cancellation token; loop breaks as soon as it is requested.</param>
        internal static void DrainExport(Func<int> readChunk, Action<long> onProgress, CancellationToken ct) {
            long totalWritten = 0;
            int chunkRead;
            while ((chunkRead = readChunk()) > 0) {
                if (ct.IsCancellationRequested) break;
                totalWritten += chunkRead;
                onProgress(totalWritten);
            }
        }

        public void OnOpenFolder(object? sender, RoutedEventArgs args) {
            string path = OutputPathBox.Text ?? "";
            if (!string.IsNullOrEmpty(path)) {
                try {
                    string? dir = Path.GetDirectoryName(path);
                    if (dir != null && Directory.Exists(dir))
                        OS.OpenFolder(dir);
                } catch { }
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            try { _cts?.Cancel(); } catch { }
        }
    }
}
