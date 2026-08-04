using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OpenUtau.App.Controls;
using OpenUtau.App.ViewModels;
using OpenUtau.Colors;
using OpenUtau.Core;
using OpenUtau.Core.Vst;

namespace OpenUtau.App.Views {
    public partial class PreferencesDialog : UserControl {
        private PreferencesViewModel? viewModel => this.DataContext as PreferencesViewModel;

        /// <summary>The window hosting this control in its overlay.</summary>
        public Window? HostWindow { get; set; }

        public PreferencesDialog() {
            InitializeComponent();
            // B3：SettingsLayout.Items 只读 DirectProperty，XamlIl 集合填充会 NRE——
            // item 声明在隐藏容器，构造后统一赋值（setter → SetAndRaise）
            PrefsLayout.Items = PrefsItemsHost.Children.Cast<SukiUI.Controls.SettingsLayoutItem>().ToArray();
            BuildPlusFooter();
        }

        void BuildPlusFooter() {
            var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            string versionText = ver == null ? "" : $"v{ver.Major}.{ver.Minor}.{ver.Build} p{OpenUtau.Core.PlusInfo.PlusVersion}";
            PlusFooter1.Text = string.Format(ThemeManager.GetString("prefs.plus.footer1"), versionText);
            PlusFooter2.Text = ThemeManager.GetString("prefs.plus.footer2");
        }

        void OpenReadme(object sender, RoutedEventArgs e) {
            // 默认关联打开本地 README（安装版在安装目录，开发/便携版经 csproj 复制到输出）
            string path = System.IO.Path.Combine(OpenUtau.Core.PathManager.Inst.RootPath, "README.md");
            if (File.Exists(path)) {
                try {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    return;
                } catch (Exception ex) {
                    Log.Error(ex, "[Prefs] Failed to open README");
                }
            }
            // 缺失时降级打开 GitHub 页
            OpenGithub(sender, e);
        }

        void OpenGithub(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://github.com/XKLMY-hi/OpenUTAU-Plus") { UseShellExecute = true });
            } catch (Exception ex) {
                Log.Error(ex, "[Prefs] Failed to open GitHub");
            }
        }

        void OpenSingersFolder(object sender, RoutedEventArgs e) {
            try {
                Directory.CreateDirectory(viewModel!.SingerPath);
                OS.OpenFolder(viewModel!.SingerPath);
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OpenAddlSingersFolder(object sender, RoutedEventArgs e) {
            try {
                if (Directory.Exists(viewModel!.AdditionalSingersPath)) {
                    OS.OpenFolder(viewModel!.AdditionalSingersPath);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void ResetAddlSingersPath(object sender, RoutedEventArgs e) {
            viewModel!.SetAddlSingersPath(string.Empty);
        }

        async void SelectAddlSingersPath(object sender, RoutedEventArgs e) {
            var path = await FilePicker.OpenFolderAboutSinger(HostWindow!, "prefs.paths.addlsinger");
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (Directory.Exists(path)) {
                viewModel!.SetAddlSingersPath(path);
            }
        }

        void OpenSamplesFolder(object sender, RoutedEventArgs e) {
            try {
                if (Directory.Exists(viewModel!.SamplesPath)) {
                    OS.OpenFolder(viewModel!.SamplesPath);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void ResetSamplesPath(object sender, RoutedEventArgs e) {
            viewModel!.SetSamplesPath(string.Empty);
        }

        async void SelectSamplesPath(object sender, RoutedEventArgs e) {
            var path = await FilePicker.OpenFolderAboutSinger(HostWindow!, "prefs.paths.samples");
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (Directory.Exists(path)) {
                viewModel!.SetSamplesPath(path);
            }
        }

        async void ReloadSingers(object sender, RoutedEventArgs e) {
            LoadingWindow.BeginLoading(HostWindow!);
            await Task.Run(() => {
                SingerManager.Inst.SearchAllSingers();
            });
            DocManager.Inst.ExecuteCmd(new SingersRefreshedNotification());
            LoadingWindow.EndLoading();
        }

        void ResetVLabelerPath(object sender, RoutedEventArgs e) {
            viewModel!.SetVLabelerPath(string.Empty);
        }

        async void SelectVLabelerPath(object sender, RoutedEventArgs e) {
            var type = OS.IsWindows() ? FilePicker.EXE : OS.IsMacOS() ? FilePicker.APP : FilePickerFileTypes.All;
            var path = await FilePicker.OpenFile(HostWindow!, "prefs.advanced.vlabelerpath", type);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (OS.AppExists(path)) {
                viewModel!.SetVLabelerPath(path);
            }
        }

        void ResetSetParamPath(object sender, RoutedEventArgs e) {
            viewModel!.SetSetParamPath(string.Empty);
        }

        async void SelectSetParamPath(object sender, RoutedEventArgs e) {
            var path = await FilePicker.OpenFile(HostWindow!, "prefs.otoeditor.setparampath", FilePicker.EXE);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (File.Exists(path)) {
                viewModel!.SetSetParamPath(path);
            }
        }

        void ResetWinePath(object sender, RoutedEventArgs e) {
            ((PreferencesViewModel)DataContext!).SetWinePath(string.Empty);
        }

        async void SelectWinePath(object sender, RoutedEventArgs e) {
            var path = await FilePicker.OpenFile(HostWindow!, "prefs.advanced.winepath", FilePicker.UnixExecutable);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            if (File.Exists(path)) {
                ((PreferencesViewModel)DataContext!).SetWinePath(path);
            }
        }

        void DetectWinePath(object sender, RoutedEventArgs e) {
            string[] wineNames = { "wine", "wine64", "wine32", "wine32on64" };
            string winePath = string.Empty;

            foreach (string wineName in wineNames) {
                winePath = OS.WhereIs(wineName);
                if (!string.IsNullOrEmpty(winePath)) {
                    break;
                }
            }

            if (string.IsNullOrEmpty(winePath)) {
                return;
            }

            ((PreferencesViewModel)DataContext!).SetWinePath(winePath);
        }

        void OpenCustomThemeEditor(object sender, RoutedEventArgs e) {
            if (CustomTheme.IsPackageTheme(viewModel!.ThemeName)) return;
            ThemeEditorWindow.Show(CustomTheme.Themes[viewModel!.ThemeName]);
        }

        void OnCustomThemeCreate(object sender, RoutedEventArgs e) {
            var dialog = new TypeInDialog {
                Title = ThemeManager.GetString("prefs.appearance.customtheme.create.title")
            };
            dialog.SetPrompt(ThemeManager.GetString("prefs.appearance.customtheme.create.prompt"));
            dialog.onFinish = s => {
                if (string.IsNullOrEmpty(s)) {
                    MessageBox.ShowModal(HostWindow!,
                        ThemeManager.GetString("prefs.appearance.customtheme.create.empty"),
                        ThemeManager.GetString("prefs.appearance.customtheme.create.title"));
                    return;
                }

                string filename = string.Join("", s.Where(c => Char.IsLetterOrDigit(c) || c == ' '))
                                        .Replace(" ", "-").ToLower() + ".yaml";

                var themeYaml = new CustomTheme.ThemeYaml { Name = s };

                File.WriteAllText(Path.Join(PathManager.Inst.ThemesPath, filename),
                    Yaml.DefaultSerializer.Serialize(themeYaml));
                viewModel!.RefreshThemes();
            };
            dialog.ShowDialog(HostWindow!);
        }

        // ── OpenUTAU Plus: VST Settings ─────────────────────
        void AddVstPath(object sender, RoutedEventArgs e) {
            var path = NewVstPath.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(path)) {
                viewModel!.AddVstScanPath(path);
                NewVstPath.Text = "";
            }
        }
        void RemoveVstPath(object sender, RoutedEventArgs e) {
            if (VstPathsList.SelectedItem is string path)
                viewModel!.RemoveVstScanPath(path);
        }
        void RescanVstPlugins(object sender, RoutedEventArgs e) {
            viewModel!.RefreshVstPlugins();
        }

        async void OnCustomThemeDelete(object sender, RoutedEventArgs e) {
            if (CustomTheme.IsPackageTheme(viewModel!.ThemeName)) return;
            var result = await MessageBox.Show(
                HostWindow!,
                ThemeManager.GetString("prefs.appearance.customtheme.delete.message"),
                ThemeManager.GetString("prefs.appearance.customtheme.delete.title"),
                MessageBox.MessageBoxButtons.YesNo);
            if (result == MessageBox.MessageBoxResult.Yes) {
                string previousTheme = viewModel!.ThemeItems.TakeWhile(x => x != viewModel!.ThemeName).LastOrDefault()!;
                File.Delete(CustomTheme.Themes[viewModel!.ThemeName]);
                viewModel!.RefreshThemes();
                viewModel!.ThemeName = previousTheme;
            }
        }
    }
}
