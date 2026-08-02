using System.Reflection;
using Xunit;
using Avalonia.Headless.XUnit;
using OpenUtau.App.Views;
using SukiUI.Controls;

namespace OpenUtau.App;

/// <summary>
/// B1b 探针：SukiWindow 派生窗口在编译 XAML 路径下的 x:Name 字段填充验证。
/// SukiTrial 中 x:Name 字段为 null 的坑源于手动 AvaloniaXamlLoader.Load(this)
/// （运行时加载，编译器不生成字段赋值）——本项目用标准 XAML 编译，理论上无此问题。
/// 此测试实证：MessageBox（WindowEx : SukiWindow 派生，axaml 大量 x:Name 引用）字段必须非 null。
/// </summary>
public class WindowSukiProbeTests {
    [AvaloniaFact]
    public void WindowEx_IsSukiWindow() {
        var msgbox = new MessageBox();
        Assert.IsAssignableFrom<SukiWindow>(msgbox);
    }

    [AvaloniaFact]
    public void XamlCompiled_NamesAreFilled() {
        var msgbox = new MessageBox();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var name in new[] { "Text", "TextPanel", "Buttons" }) {
            var field = typeof(MessageBox).GetField(name, flags);
            Assert.NotNull(field);
            Assert.NotNull(field.GetValue(msgbox));
        }
    }

    [AvaloniaFact]
    public void LoadingWindow_Instantiates() {
        var win = new LoadingWindow();
        Assert.NotNull(win);
    }
}
