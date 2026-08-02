using Xunit;
using Avalonia.Headless.XUnit;
using OpenUtau.App.Views;
using SukiUI.Controls;

namespace OpenUtau.App;

/// <summary>
/// B1b 探针（阶段 D 更新）：SukiWindow 派生窗口链验证。
/// 原 MessageBox x:Name 实证对象已随阶段 D 重构退役（MessageBox 不再继承 WindowEx，
/// 改为 SukiMessageBox 门面）；x:Name 填充验证由 PreferencesDialogProbeTests 覆盖。
/// </summary>
public class WindowSukiProbeTests {
    [AvaloniaFact]
    public void WindowEx_IsSukiWindow() {
        var win = new LoadingWindow();
        Assert.IsAssignableFrom<SukiWindow>(win);
    }

    [AvaloniaFact]
    public void LoadingWindow_Instantiates() {
        var win = new LoadingWindow();
        Assert.NotNull(win);
    }
}
