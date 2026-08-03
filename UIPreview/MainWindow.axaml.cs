using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SukiUI.Controls;

namespace UIPreview;

/// <summary>阶段 E 布局预览：侧栏折叠/展开 + 主选项卡（项目/素材库）+ 素材库子选项卡（歌手/采样/VST）。</summary>
public partial class MainWindow : SukiWindow {
    private bool _sidebarCollapsed;

    public MainWindow() {
        InitializeComponent();
    }

    private void OnToggleSidebar(object? sender, RoutedEventArgs e) {
        _sidebarCollapsed = !_sidebarCollapsed;
        if (_sidebarCollapsed) {
            MainLayout.ColumnDefinitions[0].Width = new GridLength(0);
            MainLayout.ColumnDefinitions[1].Width = new GridLength(0);
            SidebarSplitter.IsVisible = false;
            SidebarCloseIcon.IsVisible = false;
            SidebarOpenIcon.IsVisible = true;
        } else {
            MainLayout.ColumnDefinitions[0].Width = new GridLength(208);
            MainLayout.ColumnDefinitions[1].Width = new GridLength(6);
            SidebarSplitter.IsVisible = true;
            SidebarCloseIcon.IsVisible = true;
            SidebarOpenIcon.IsVisible = false;
        }
    }

    // ── 主选项卡：项目 / 素材库 ──
    private void OnShowProjects(object? sender, RoutedEventArgs e) {
        ProjectPanel.IsVisible = true;
        LibraryPanel.IsVisible = false;
        SetSelected(ProjectsTab, new[] { ProjectsTab, LibraryTab });
    }

    private void OnShowLibrary(object? sender, RoutedEventArgs e) {
        ProjectPanel.IsVisible = false;
        LibraryPanel.IsVisible = true;
        SetSelected(LibraryTab, new[] { ProjectsTab, LibraryTab });
    }

    // ── 素材库子选项卡：歌手 / 采样 / VST ──
    private void OnShowSingers(object? sender, RoutedEventArgs e) {
        SingersPanel.IsVisible = true;
        SamplesPanel.IsVisible = false;
        VstPanel.IsVisible = false;
        SetSelected(SingersTab, new[] { SingersTab, SamplesTab, VstTab });
    }

    private void OnShowSamples(object? sender, RoutedEventArgs e) {
        SingersPanel.IsVisible = false;
        SamplesPanel.IsVisible = true;
        VstPanel.IsVisible = false;
        SetSelected(SamplesTab, new[] { SingersTab, SamplesTab, VstTab });
    }

    private void OnShowVst(object? sender, RoutedEventArgs e) {
        SingersPanel.IsVisible = false;
        SamplesPanel.IsVisible = false;
        VstPanel.IsVisible = true;
        SetSelected(VstTab, new[] { SingersTab, SamplesTab, VstTab });
    }

    private static void SetSelected(Button selected, Button[] group) {
        foreach (var button in group) {
            button.Classes.Set("selected", button == selected);
        }
    }
}
