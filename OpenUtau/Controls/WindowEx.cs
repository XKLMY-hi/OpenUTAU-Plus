using Avalonia.Controls;
using SukiUI.Controls;

namespace OpenUtau.App.Controls;

/// <summary>
/// Window base class. 继承 SukiWindow：SukiUI 自绘标题栏 + 渐变背景（跟随主题色）。
/// 背景固定不透明：SukiWindow 的圆角/阴影在窗口内容内自绘，禁止 OS 级透明合成。
/// </summary>
public class WindowEx : SukiWindow
{
    public WindowEx()
    {
        // 固定不透明：窗口背景由 SukiWindow BackgroundStyle 自绘（随主题色变化）
        TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
    }
}
