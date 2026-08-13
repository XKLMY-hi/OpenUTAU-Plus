# 混音台内嵌化 — 钢琴卷帘双模式同款

## Context

当前混音台只能是独立浮动窗口，不像钢琴卷帘那样可以内嵌在主窗口
或分离为独立窗口。参照钢琴卷帘的 `SetPianoRollAttachment()` 模式，
让混音台也支持内嵌 + 分离双模式。

## 核心思路

复用钢琴卷帘的 reparent 模式：
- 提取一个 `MixerControl` (UserControl)，持有混音台的全部逻辑
  （ViewModel、TrackStripsPanel、VU 定时器、按键处理）
- 该控件只有一个实例，在 MainWindow 和 MixerDetachedWindow 之间移动
- `MixerDetachedWindow` 只是一个空壳 WindowEx，包装 MixerControl

## 涉及文件

| 文件 | 改动 |
|------|------|
| `Controls/MixerControl.axaml` | **新建** — 混音台核心控件 XAML |
| `Controls/MixerControl.axaml.cs` | **新建** — 混音台核心逻辑（从 MixerWindow.axaml.cs 移过来）|
| `Views/MixerWindow.axaml` | **重写** — 变为空壳，只含 WindowTitleBar + MixerContainer |
| `Views/MixerWindow.axaml.cs` | **重写** — 接收 MixerControl 实例，生命周期转发 |
| `Views/MainWindow.axaml` | 加 MixerContainer ContentControl + ShowMixer 绑定 |
| `Views/MainWindow.axaml.cs` | 加 mixerControl/mixerWindow 字段 + SetMixerAttachment |
| `ViewModels/MainWindowViewModel.cs` | 加 ShowMixer 属性 |
| `Core/Util/Preferences.cs` | 加 DetachMixer + MixerWindowSize 字段 |

## 实现步骤

### Step 1: 提取 MixerControl

从 `MixerWindow.axaml.cs` 提取全部逻辑到新的 `MixerControl : UserControl`：

```csharp
public partial class MixerControl : UserControl {
    internal readonly MixerViewModel ViewModel;
    private readonly DispatcherTimer levelTimer;

    public MixerControl() {
        InitializeComponent();
        DataContext = ViewModel = new MixerViewModel();
        RebuildStrips();
        ViewModel.Tracks.CollectionChanged += (_, _) => RebuildStrips();
        // KeyDown → Space forward to main window
        // VU timer 30fps
        levelTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), ...);
        levelTimer.Start();
    }
    public void OnClosed() { /* stop timer, unsubscribe */ }
    public void RebuildStrips() { /* move from MixerWindow */ }
    // etc.
}
```

XAML 就是把 MixerWindow 的内容（TrackStripsPanel ScrollViewer + StatusBar）搬过来。

### Step 2: 重写 MixerWindow 为轻量壳

```csharp
public partial class MixerWindow : WindowEx {
    private readonly MixerControl mixerControl;
    private bool forceClose;

    public MixerWindow(MixerControl mixerControl) {
        InitializeComponent();
        this.mixerControl = mixerControl;
        MixerContainer.Content = mixerControl;  // reparent
        Title = mixerControl.ViewModel.HasProject ? "Mixer" : "...";
        // 恢复窗口位置
    }
    public void ForceClose() { forceClose = true; Close(); }
    // OnClosed → 保存窗口位置 → Hide (除非 forceClose)
}
```

### Step 3: MainWindow.axaml 加内嵌容器

在编辑器 Grid 的合适位置（建议在 Row 2 ~ Row 4 之间或底部），
加一个 `ContentControl Name="MixerContainer"` 绑定 `IsVisible="{Binding ShowMixer}"`。

与钢琴卷帘不同的是，混音台最好是**水平条状**，放在编辑器底部
或作为一个可折叠的底部面板（类似 FL Studio / Reaper 的混音台）。

### Step 4: MainWindowViewModel 加 ShowMixer 属性

```csharp
[Reactive] public bool ShowMixer { get; set; }
```

### Step 5: Preferences 加持久化字段

```csharp
public bool DetachMixer = false;
public WindowSize MixerWindowSize = new WindowSize();
```

### Step 6: MainWindow.axaml.cs 加切换逻辑

参照 `SetPianoRollAttachment()`，实现 `SetMixerAttachment()`：

```csharp
private MixerControl? mixerControl;
private MixerWindow? mixerWindow;

public void SetMixerAttachment() {
    if (mixerControl == null) return;
    if (Preferences.Default.DetachMixer) {
        // 分离 → 内嵌
        mixerWindow?.ForceClose();
        mixerWindow = null;
        MixerContainer.Content = mixerControl;
        viewModel.ShowMixer = true;
        Preferences.Default.DetachMixer = false;
    } else {
        // 内嵌 → 分离
        MixerContainer.Content = null;
        viewModel.ShowMixer = false;
        mixerWindow = new MixerWindow(mixerControl);
        mixerWindow.Show();
        Preferences.Default.DetachMixer = true;
    }
    Preferences.Save();
}
```

Ctrl+M 首次打开时创建 MixerControl，根据 DetachMixer 决定模式。

## 验证

1. `dotnet build` 通过
2. 启动 → 打开项目 → Ctrl+M 首次打开混音台 → 默认内嵌在主窗口底部
3. 菜单/按钮切换分离模式 → 弹出独立窗口，内嵌区隐藏
4. 分离窗口关闭/切回 → 控件回到内嵌区
5. 分离窗口重启应用后记忆位置
6. VU 表、推子、静音/独奏、FX 按钮全部功能正常
