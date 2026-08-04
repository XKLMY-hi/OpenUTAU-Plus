using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenUtau.App.Controls;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;

namespace OpenUtau.App.Views {
    /// <summary>
    /// 阶段 E3：欢迎窗口（独立工程管理界面）。
    /// 启动时先显示本窗口；新建/打开工程后创建并显示 MainWindow（编辑界面），本窗口关闭。
    /// DataContext 为独立 MainWindowViewModel 实例（构造函数不依赖窗口，直接提供
    /// RecentFiles/TemplateFiles/恢复状态）。
    /// 继承 WindowEx（Suki 边框收敛：无标题栏分界线/无底部线）——须与 MainWindow 一致。
    /// </summary>
    public partial class WelcomeWindow : WindowEx {
        private readonly MainWindowViewModel _viewModel;

        public WelcomeWindow() {
            InitializeComponent();
            DataContext = _viewModel = new MainWindowViewModel();
            _viewModel.InitProject(); // 恢复状态（HasRecovery/RecoveryString）

            // 命令行动作：OpenUTAU Plus <file> 启动即打开工程
            var args = Environment.GetCommandLineArgs();
            if (args.Length == 2 && File.Exists(args[1])) {
                Opened += (_, _) => OpenProjectPath(args[1]);
            }
        }

        // ── 创建主编辑窗口（desktop.MainWindow 必须指向 MainWindow：TrackHeaderViewModel 等依赖） ──
        private MainWindow CreateMainWindow() {
            // 复用已有主编辑器：Avalonia 的 MainWindow setter 会关闭旧 MainWindow
            //（OnMainWindowClose 语义）——再次打开项目时旧主编辑器会被自动关闭
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow existing && existing.IsVisible) {
                return existing;
            }
            var mainWindow = new MainWindow();
            mainWindow.Show();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2) {
                desktop2.MainWindow = mainWindow;
            }
            return mainWindow;
        }

        private void OpenProjectPath(string path) {
            var main = CreateMainWindow();
            main.OpenProjectFiles(new[] { path });
            Close();
        }

        // ── 动作 ──
        void OnNewProject(object? sender, PointerPressedEventArgs args) {
            CreateMainWindow(); // MainWindow 构造函数已加载默认模板/空工程
            Close();
        }

        async void OnOpenProject(object? sender, PointerPressedEventArgs args) {
            var files = await FilePicker.OpenFilesAboutProject(
                this, "menu.file.open",
                FilePicker.ProjectFiles,
                FilePicker.USTX,
                FilePicker.VSQX,
                FilePicker.UST,
                FilePicker.MIDI,
                FilePicker.UFDATA,
                FilePicker.MUSICXML);
            if (files == null || files.Length == 0) {
                return;
            }
            var main = CreateMainWindow();
            main.OpenProjectFiles(files);
            Close();
        }

        void OnImportAudio(object? sender, PointerPressedEventArgs args) {
            var main = CreateMainWindow();
            main.ImportAudio();
            Close();
        }

        void OnOpenRecent(object? sender, PointerPressedEventArgs args) {
            if (sender is StyledElement el && el.DataContext is RecentFileInfo fileInfo) {
                var main = CreateMainWindow();
                main.OpenProjectFiles(new[] { fileInfo.PathName });
                Close();
            }
        }

        void OnOpenTemplate(object? sender, PointerPressedEventArgs args) {
            if (sender is StyledElement el && el.DataContext is RecentFileInfo fileInfo) {
                var main = CreateMainWindow();
                main.OpenTemplateFile(fileInfo.PathName);
                Close();
            }
        }

        void OnRecovery(object? sender, RoutedEventArgs args) {
            OpenProjectPath(_viewModel.RecoveryPath);
        }

        // ── 链接 ──
        void OnPreferences(object? sender, RoutedEventArgs args) {
            var main = CreateMainWindow();
            main.ShowPreferences();
            Close();
        }

        void OnPackages(object? sender, PointerPressedEventArgs args) {
            var main = CreateMainWindow();
            main.ShowPackageManager();
            Close();
        }

        void OnCheckUpdate(object? sender, PointerPressedEventArgs args) {
            // MainWindow 构造函数会触发更新检查（overlay 弹窗）
            CreateMainWindow();
            Close();
        }

        void OnWiki(object? sender, PointerPressedEventArgs args) {
            try { OS.OpenWeb("https://github.com/stakira/OpenUtau/wiki/Getting-Started"); }
            catch (Exception e) { DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e)); }
        }

        void OnReportIssue(object? sender, PointerPressedEventArgs args) {
            try { OS.OpenWeb("https://github.com/XKLMY-hi/OpenUTAU-Plus/issues"); }
            catch (Exception e) { DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e)); }
        }
    }
}
