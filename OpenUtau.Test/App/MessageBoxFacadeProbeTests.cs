using Xunit;
using Avalonia.Headless.XUnit;
using SukiUI.Controls;
using SukiUI.MessageBox;

namespace OpenUtau.App;

/// <summary>
/// 阶段 D 探针：MessageBox 门面（SukiMessageBox 渲染）的关键契约——
/// ButtonsFactory 按钮 Tag 携带 Result（点击后 SukiMessageBox 按 Tag.Result 关闭窗口并返回），
/// 门面 Show 的返回值映射依赖此契约。
/// </summary>
public class MessageBoxFacadeProbeTests {
    [AvaloniaFact]
    public void Factory_ButtonTag_CarriesResult() {
        var ok = SukiMessageBoxButtonsFactory.CreateButton("OK", SukiMessageBoxResult.OK, "Flat");
        var tag = Assert.IsType<SukiMessageBoxButtonTag>(ok.Tag);
        Assert.Equal(SukiMessageBoxResult.OK, tag.Result);
        Assert.Equal("OK", ok.Content as string);
    }

    [AvaloniaFact]
    public void Factory_ButtonTag_WithoutResult() {
        // 复制按钮（无 result）：点击后 SukiMessageBox 关闭窗口并返回 sender，不映射结果
        var copy = SukiMessageBoxButtonsFactory.CreateButton("Copy", null, "Flat");
        var tag = Assert.IsType<SukiMessageBoxButtonTag>(copy.Tag);
        Assert.Null(tag.Result);
    }

    [AvaloniaFact]
    public void Host_Assembles_IconAndContent() {
        var host = new SukiMessageBoxHost {
            Header = "title",
            IconPreset = SukiMessageBoxIcons.Error,
            Content = "message",
        };
        Assert.Equal(SukiMessageBoxIcons.Error, host.IconPreset);
        Assert.Equal("message", host.Content);
    }
}
