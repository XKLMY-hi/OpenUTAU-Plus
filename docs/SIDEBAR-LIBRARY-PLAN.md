# 侧栏素材库功能 Plan（E4）

> 状态：已确认 · 2026-08-03
> 范围：侧栏「素材库」真实功能——歌手列表 + 伴奏库。VST 占位保留（未来）。

## 一、用户决策记录（拍板）

| # | 决策 |
|---|---|
| 1 | **歌手卡片**：双击 → 新建轨道添加歌手；**拖拽**到主编辑器 → 新建轨道添加歌手；**无需试听** |
| 2 | **采样库不做**（未来采样器再说）→ 现阶段改为**伴奏库**（BGM）：双击 → 新建轨道添加音频；卡片右侧**试听按钮**；拖拽 → 新建轨道添加音频 |
| 3 | **引擎铭牌只显示 Classic / DiffSinger**（其他引擎不显示铭牌） |
| 4 | **歌手头像 = 圆角矩形**（非圆形） |
| 5 | **伴奏识别性能**：仅枚举文件夹内音频格式文件创建卡片（文件名+路径），**不读元数据/不加载**；试听与添加时才读取 |

## 二、功能规格

### 2.1 歌手列表（SidebarViewModel 提供）
- 数据：`SingerManager.Inst.SingerGroups` 扁平（已按引擎分组排序）→ `ObservableCollection<USinger>`
- 实时更新：`ICmdSubscriber` 订阅 `SingersRefreshedNotification`（歌手库变化自动刷新）
- 刷新：`SearchAllSingers()` + `SingersRefreshedNotification`（复制 TrackHeaderViewModel 刷新命令）
- 卡片：**圆角矩形** 34px 头像（`AvatarData` → Bitmap，无图时首字符回退）+ `LocalizedName` + 引擎铭牌（仅 Classic/DiffSinger）
- 交互：**双击** → 新建轨道+添加歌手；**拖拽**（自定义数据格式）→ 主编辑器 Drop 新建轨道+添加歌手

### 2.2 伴奏库（原采样库改造）
- 数据源（新建）：
  - `Preferences.SampleSearchPaths`（新设置项）
  - `PathManager.SamplesPath`（新路径，默认用户文档下 Samples）
  - 扫描器：枚举目录内 wav/mp3/ogg/opus/flac **仅文件名**（性能：不读时长/元数据）
- 卡片：icon-music 方块 + 文件名 + 右侧**试听按钮**（`PlaybackManager.Inst.PlayFile`）
- 交互：**双击** → 新建轨道+添加音频；**拖拽** → 主编辑器 Drop 新建轨道+添加音频
- 刷新按钮（重扫目录）

### 2.3 VST 乐器音源
- 占位保留，不动

## 三、实现方案

### 3.1 SidebarViewModel（新建，OpenUtau/ViewModels/SidebarViewModel.cs）
```csharp
public class SidebarViewModel : ViewModelBase, ICmdSubscriber {
    // 歌手
    public ObservableCollection<USinger> Singers { get; }
    public Bitmap? LoadAvatar(USinger singer);   // 复制 SingersViewModel.LoadAvatar
    public void RefreshSingers();                // SearchAllSingers + 通知
    // 伴奏
    public ObservableCollection<SampleItem> Samples { get; }  // { Name, Path }
    public void RefreshSamples();                // 枚举目录音频文件（不读元数据）
    // 通知
    public void OnNext(UCommand cmd, bool isUndo); // SingersRefreshedNotification → RefreshSingers
}
```
- 由 MainWindow 构造：`_sidebarVm = new SidebarViewModel()` + `DocManager.Inst.AddSubscriber(_sidebarVm)`
- 侧栏 DataContext 绑定 `_sidebarVm`（或并入现有绑定）

### 3.2 交互命令（新建轨道+添加）
- 歌手轨道：`DocManager` 命令链（可撤销）：
  ```csharp
  StartUndoGroup("command.track.add");
  AddTrackCommand(project, new UTrack(project) { TrackNo = n, Singer = singer, Phonemizer = ... });
  EndUndoGroup();
  ```
  ⚠️ 音素器初始化参照现有建轨路径（TrackHeader 歌手选择后如何设 Phonemizer）
- 音频轨道：复用 `MainWindowViewModel.ImportAudio`（AddTrackCommand + AddPartCommand 已完整）

### 3.3 拖拽
- 卡片 `PointerPressed` + 移动阈值 → `DragDrop.DoDragDrop`（自定义格式 `"OpenUtau.Singer"` / `"OpenUtau.Audio"`，数据 = USinger Id / 文件路径）
- 主编辑器 `OnDrop` 扩展：先检查自定义格式 → 新建轨道添加；否则原文件拖入逻辑（现有 OnDrop 保留）
- ⚠️ 拖拽目标区：编辑页 Grid（PartsCanvas 区域）——确认 Drop 事件挂载点

### 3.4 侧栏 UI（MainWindow.axaml）
- `SingersPanel`：占位 → ListBox + ItemTemplate（sideItem 样式；圆角矩形头像；铭牌 Border 仅 Classic/DiffSinger 可见）
- `SamplesPanel`：占位 → ListBox + ItemTemplate（icon-music 方块 + 文件名 + 右侧试听 iconBtn）
- 新样式：圆角矩形头像 Border（CornerRadius 6-8）、铭牌样式

## 四、实施步骤（原子提交）

1. **SidebarViewModel**：歌手/伴奏集合 + 头像 + 刷新 + 通知订阅 + 扫描器
2. **Preferences/PathManager**：SampleSearchPaths + SamplesPath（+ 偏好设置 UI 项）
3. **歌手 UI**：SingersPanel 真实列表（圆角头像/铭牌/双击/拖拽）
4. **伴奏 UI**：SamplesPanel 真实列表（试听按钮/双击/拖拽）
5. **MainWindow 对接**：拖拽 Drop 处理 + 新建轨道添加命令 + 侧栏 VM 挂载
6. **文案资源**（英+中）

## 五、风险与待验证

| 项 | 说明 |
|---|---|
| 新建歌手轨道的音素器设置 | 参照现有建轨/歌手选择路径（TrackHeader 设歌手后 Phonemizer 如何初始化） |
| 拖拽 Drop 挂载点 | 编辑页 Drop 区域确认（现有 OnDrop 在窗口级） |
| 伴奏目录默认值 | 首次无设置时：提示设置 or 默认用户文档/Samples |
| 头像性能 | 歌手多时 AvatarData 已预载内存（构造即读），列表渲染 OK；头像 Bitmap 缩放按需 |

## 六、不做（本次）

- 采样器（未来）
- 歌手语言显示（铭牌只引擎类型）
- 歌手搜索/收藏操作（后续迭代可加）
