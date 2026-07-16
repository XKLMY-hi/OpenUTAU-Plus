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
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using NAudio.Wave;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.App.Views {
    public partial class RenderWindow : Window {
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
            ProgressLabel.Text = "正在准备渲染...";
            ProgressSubLabel.IsVisible = true;
            ProgressBarControl.Value = 0;

            bool isMixdown = RadioMixdown.IsChecked == true;
            bool silentRecord = ChkSilentRecord.IsChecked == true;
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
                        // silentRecord captured above (before Task.Run)

                        if (silentRecord) {
                            // ── 实时录制模式：渲染→模拟播放写入 ──────
                            Dispatcher.UIThread.Invoke(() => {
                                ProgressLabel.Text = "正在渲染混缩...";
                                ProgressBarControl.Value = 20;
                            });

                            var renderResult = engine.RenderProject(
                                DocManager.Inst.MainScheduler, ref ctx);
                            var masterAdapter = renderResult.Item1;

                            Dispatcher.UIThread.Invoke(() => {
                                ProgressLabel.Text = "正在写入录音文件...";
                                ProgressBarControl.Value = 70;
                            });

                            // Use RecordingAdapter to write silently to file
                            using var writer = new WaveFileWriter(
                                File.Create(path), WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                            var recorder = new RecordingAdapter(masterAdapter, writer, silentOutput: true);

                            float[] buf = new float[4096];
                            int totalRead = 0;
                            while ((totalRead = recorder.Read(buf, 0, buf.Length)) > 0) {
                                // pull all data — RecordingAdapter writes to file automatically
                                var sec = totalRead / 44100.0 / 2.0;
                                if (totalRead % 88200 == 0) {
                                    Dispatcher.UIThread.Invoke(() => {
                                        ProgressBarControl.Value = 70 + Math.Min(25, sec / 60.0 * 25);
                                    });
                                }
                            }
                        } else {
                            // ── Offline mixdown (fast, RenderMixdown) ──
                            Dispatcher.UIThread.Invoke(() => {
                                ProgressLabel.Text = "正在渲染混缩...";
                                ProgressBarControl.Value = 20;
                            });

                            var result = engine.RenderMixdown(
                                DocManager.Inst.MainScheduler, ref ctx,
                                wait: true, applyMixFx: applyMixFx);

                            Dispatcher.UIThread.Invoke(() => {
                                ProgressLabel.Text = "正在写入文件...";
                                ProgressBarControl.Value = 80;
                            });

                            WriteWavFile(path, result.Item1);
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
                                ProgressLabel.Text = $"导出轨道 {idx + 1}/{total}: {track.TrackName}";
                                ProgressBarControl.Value = 20 + (60 * idx / total);
                            });

                            string trackPath = Path.Combine(
                                Path.GetDirectoryName(path) ?? "",
                                $"{Path.GetFileNameWithoutExtension(path)}_{Sanitize(track.TrackName)}.wav");
                            WriteWavFile(trackPath, trackMixes[i]);
                        }
                    }

                    Dispatcher.UIThread.Invoke(() => {
                        ProgressLabel.Text = "渲染完成！";
                        ProgressSubLabel.IsVisible = false;
                        ProgressBarControl.Value = 100;
                    });
                } catch (Exception ex) {
                    Log.Error(ex, "[RenderWindow] Render failed");
                    Dispatcher.UIThread.Invoke(() => {
                        ProgressLabel.Text = "渲染失败。";
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
