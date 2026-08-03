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
using OpenUtau.Core.Export;
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

            Task.Run(async () => {
                try {
                    if (isMixdown) {
                        // ── 录制式混音导出：设备播放驱动，与预览完全同路径 ──
                        //（同一信号链/VST 激活时序——导出的就是预览听到的）
                        await PlaybackManager.Inst.RecordMixdown(project, path, 0, -1,
                            new Progress<double>(p => Dispatcher.UIThread.Invoke(() => {
                                if (p >= 1) {
                                    ProgressLabel.Text = ThemeManager.GetString("render.status.done");
                                    ProgressSubLabel.IsVisible = false;
                                    ProgressBarControl.Value = 100;
                                } else {
                                    ProgressLabel.Text = p < 0.4
                                        ? ThemeManager.GetString("render.status.mixdown")
                                        : ThemeManager.GetString("render.status.writing");
                                    ProgressBarControl.Value = p * 100;
                                }
                            })), ctx.Token);
                    } else {
                        var session = new ExportSession(project, path,
                            new ExportSession.Options {
                                PerTrack = true,
                                ApplyMixFx = false,
                            }, PlaybackManager.Inst.PhraseCache);

                        session.RunAsync(new Progress<ExportSession.ProgressInfo>(info => {
                            Dispatcher.UIThread.Invoke(() => {
                                if (info.Percent >= 1) {
                                    ProgressLabel.Text = ThemeManager.GetString("render.status.done");
                                    ProgressSubLabel.IsVisible = false;
                                    ProgressBarControl.Value = 100;
                                } else if (info.TrackIndex >= 0) {
                                    var track = project.tracks[info.TrackIndex];
                                    ProgressLabel.Text = string.Format(ThemeManager.GetString("render.status.exporting"), info.TrackIndex + 1, info.TrackCount, track.TrackName);
                                    ProgressBarControl.Value = 20 + (60 * info.TrackIndex / Math.Max(1, info.TrackCount));
                                } else {
                                    ProgressLabel.Text = info.Percent < 0.4
                                        ? ThemeManager.GetString("render.status.mixdown")
                                        : ThemeManager.GetString("render.status.writing");
                                    ProgressBarControl.Value = info.Percent * 100;
                                }
                            });
                        }), ctx.Token).GetAwaiter().GetResult();
                    }
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
