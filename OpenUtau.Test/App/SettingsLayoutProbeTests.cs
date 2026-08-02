using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using SukiUI.Controls;

namespace OpenUtau.App;

/// <summary>
/// 探针：SukiUI SettingsLayout 运行时视觉树结构 dump（阶段 B3 偏好设置重构参考）。
/// 模板源码在 dll 内编译不可提取，直接实例化控件观察最终渲染结构。
/// </summary>
public class SettingsLayoutProbeTests {
    static readonly string OutFile = Path.Combine(Path.GetTempPath(), "suki-settingslayout-vtree.txt");

    [AvaloniaFact]
    public void DumpSettingsLayoutVisualTree() {
        var layout = new SettingsLayout();
        var items = new SettingsLayoutItem[] {
            new() { Header = "General", Content = new TextBlock { Text = "GENERAL CONTENT" } },
            new() { Header = "Playback", Content = new TextBlock { Text = "PLAYBACK CONTENT" } },
            new() { Header = "Paths", Content = new TextBlock { Text = "PATHS CONTENT" } },
        };
        layout.Items = items;

        var win = new Window { Content = layout, Width = 800, Height = 600 };
        win.Show();
        try {
            var sb = new StringBuilder();
            DumpNode(sb, layout, 0);
            File.WriteAllText(OutFile, sb.ToString());
        } finally {
            win.Close();
        }
        Assert.True(File.Exists(OutFile));
    }

    static void DumpNode(StringBuilder sb, Visual? node, int depth) {
        if (node == null) return;
        string pad = new string(' ', depth * 2);
        string type = node.GetType().Name;
        string classes = node.Classes != null && node.Classes.Count > 0
            ? "  [classes:" + string.Join(",", node.Classes) + "]" : "";
        string name = node is StyledElement se && !string.IsNullOrEmpty(se.Name) ? "  #" + se.Name : "";
        sb.AppendLine($"{pad}{type}{name}{classes}");
        if (node is SettingsLayoutItem) return;
        foreach (var child in node.GetVisualChildren()) {
            DumpNode(sb, child, depth + 1);
        }
    }
}
