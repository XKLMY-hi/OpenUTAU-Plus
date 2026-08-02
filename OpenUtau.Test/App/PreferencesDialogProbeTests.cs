using Xunit;
using Avalonia.Headless.XUnit;
using OpenUtau.App.Views;
using SukiUI.Controls;

namespace OpenUtau.App;

/// <summary>
/// 探针：PreferencesDialog（B3 SettingsLayout 重构）XAML populate 不崩溃 +
/// Items 组装正确。上游坑：SettingsLayout.Items 只读 DirectProperty（模板前 getter null），
/// XamlIl 集合填充 getter().Add() NRE——item 声明在隐藏容器（PrefsItemsHost），
/// 构造后统一赋给 Items。布局渲染（UpdateItems）依赖 SettingsLayout 模板，
/// headless 下挂起，交给实机验证。
/// </summary>
public class PreferencesDialogProbeTests {
    [AvaloniaFact]
    public void PreferencesDialog_Instantiates_WithAssembledItems() {
        var dialog = new PreferencesDialog();
        var layout = dialog.PrefsLayout;
        Assert.NotNull(layout);
        Assert.NotNull(layout.Items);
        // 10 个分类 item 全部组装
        Assert.Equal(10, System.Linq.Enumerable.Count(layout.Items));
    }

    [AvaloniaFact]
    public void PreferencesDialog_SidebarSettings_Applied() {
        // 左侧导航强制显示：MinWidthWhetherStackSummaryShow=100（默认 1100 折叠）、
        // StackSummaryWidth=170（默认 400 太宽）
        var dialog = new PreferencesDialog();
        var layout = dialog.PrefsLayout;
        Assert.Equal(100, layout.MinWidthWhetherStackSummaryShow);
        Assert.Equal(170, layout.StackSummaryWidth);
    }
}
