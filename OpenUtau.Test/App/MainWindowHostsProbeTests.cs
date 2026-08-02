using Xunit;
using Avalonia.Headless.XUnit;
using OpenUtau.App.Controls;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace OpenUtau.App;

/// <summary>
/// 探针：B4 主窗口 Hosts 归属——SukiWindow.Hosts 集合可挂载
/// SukiDialogHost/SukiToastHost 且 Manager 可初始化（7.x host 不自动创建 manager，
/// MainWindow 构造函数里手动挂载）。MainWindow 整体实例化依赖完整应用环境
/// （DocManager 初始化），故用 WindowEx 验证同一机制。
/// </summary>
public class MainWindowHostsProbeTests {
    [AvaloniaFact]
    public void WindowEx_Hosts_AcceptDialogAndToastWithManagers() {
        var win = new WindowEx();
        win.Show();
        try {
            var dialogHost = new SukiDialogHost { Manager = new SukiDialogManager() };
            var toastHost = new SukiToastHost { Manager = new SukiToastManager() };
            win.Hosts.Add(dialogHost);
            win.Hosts.Add(toastHost);

            Assert.Equal(2, win.Hosts.Count);
            Assert.NotNull(dialogHost.Manager);
            Assert.NotNull(toastHost.Manager);
            Assert.IsType<SukiDialogManager>(dialogHost.Manager);
            Assert.IsType<SukiToastManager>(toastHost.Manager);
        } finally {
            win.Close();
        }
    }
}
