using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using SukiUI;
using SukiUI.Models;

namespace UIPreview;

public partial class App : Application {
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted() {
        // 配置 Suki 主题色（与主程序一致：Plus 暖灰）——不配置时 SukiBackground
        // 无混合色，背景渲染透明
        try {
            var suki = SukiTheme.GetInstance();
            var warmGray = new SukiColorTheme("Plus 暖灰", Color.Parse("#c73a3f"), Color.Parse("#c73a3f"));
            suki.AddColorTheme(warmGray);
            suki.ChangeBaseTheme(ThemeVariant.Dark);
            suki.ChangeColorTheme(warmGray);
        } catch { /* 预览环境忽略 */ }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
