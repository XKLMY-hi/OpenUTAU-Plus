# OpenUTAU Plus v4 — 彻底修复计划

## 问题根源分析

1. **VST编辑器没弹出** — `VstPluginHost` 从来没调 `ShowEditor()`，OwnVst3Wrapper 有这个API但代码没用
2. **VST音频不工作** — 不确定 ProcessAudio 是否真的在播放时被调到。需要加 Log 验证
3. **静音/独奏打架** — MixerChannelVm 设 `track.Mute/track.Muted` → TrackHeader的JudgeMuted又被别的路径触发覆盖 → 循环
4. **四个ComboBox没效果** — ComboBox选了插件但`SyncVstToEngine()`没被自动调用，需要选插件后自动加载+开编辑器
5. **USTXP不认** — FilePicker加了但MainWindow保存时只走.ustx路径；Load检测扩展名逻辑没加
6. **偏好表格选不中** — DataGrid绑定的是ObservableCollection<string>，需要SelectionMode保证
7. **VST扫描没全局化** — 扫描结果没存Preferences，每次都要重新扫

## 执行步骤（严格按顺序）

### 步骤1: 修复VST插件加载 + 自动显示编辑器窗口
**文件**: `OpenUtau.Core/Mixer/VstPluginHost.cs`
- 在 `Initialize()` 成功后自动调用 `plugin.ShowEditor()` 打开原生VST GUI窗口
- 加 try/catch 包裹防止崩溃
- 在 `ProcessAudio` 入口加 `Log.Debug` 打印确认被调用

### 步骤2: 修复VST信号链路 — 确保渲染时实际调用
**文件**: `OpenUtau.Core/Mixer/MixerChannel.cs`
- 在 `Mix()` 的 InsertFx foreach 循环开头加 `Log.Debug` 打印buffer前几个值确认变化

**文件**: `OpenUtau.Core/Render/RenderEngine.cs`
- 确认 VST 加载代码被执行，如果 `track.VstPluginPaths` 有值但要加log确认

### 步骤3: 彻底解决静音/独奏冲突
**根本方案**: MixerChannelVm 不再直接修改 `track.Mute/Muted/Solo`，而是**只发DocManager命令**，让TrackHeader的既有逻辑统一处理。
- MixerChannelVm.Muted变化 → 发 `TracksMuteEvent`（带正确语义）让 TrackHeaderCanvas 统一处理
- MixerChannelVm.Solo变化 → 发 `TracksSoloEvent` 让 TrackHeaderCanvas 统一处理

### 步骤4: VST ComboBox选插件时自动加载+开编辑器
**文件**: `OpenUtau.App/ViewModels/MixerViewModel.cs`
- `VstSlotVm` 的 `WhenAnyValue(SelectedIndex)` 订阅中，选完插件后立即创建 `VstPluginHost`、Initialize、调 `ShowEditor()`
- 存储已加载的 VstPluginHost 实例到 slot VM 上供后续使用

### 步骤5: 写死保存格式为.ustxp / 加载同时支持.ustx和.ustxp
**文件**: `OpenUtau.Core/Format/Ustx.cs`
- `Save()` 方法强制存为 `.ustxp`
- `Load()` 方法检测扩展名，.ustxp 时额外读 mixer 状态字段

**文件**: `OpenUtau/Views/MainWindow.axaml.cs`
- Save/SaveAs 默认扩展名改为 .ustxp
- Open 过滤器加 .ustxp

### 步骤6: 偏好设置DataGrid修复 + VST全局存储
**文件**: `OpenUtau/Views/PreferencesDialog.axaml`
- DataGrid 简化为 ListBox 替代（ListBox 选择更可靠）

**文件**: `OpenUtau.Core/Mixer/VstPluginManager.cs`
- `ScanSystem()` 完成后自动写 `Preferences.Default.VstPluginCache = Plugins.Select(p=>p.Path).ToList()`
- 构造函数从 Preferences 恢复缓存（只读路径，不加载插件）

### 步骤7: MixFx(Track Polish)按钮改为打开混音台或单轨VST窗口
**文件**: `OpenUtau/ViewModels/TrackHeaderViewModel.cs`
- `OpenMixFxDialog()` 保持不变 — 试听效果界面是好用的
- 但在该对话框中加一个按钮 "在混音台打开"

### 步骤8: 屏蔽更新 — 彻底
**文件**: `OpenUtau/ViewModels/UpdaterViewModel.cs`
- 构造函数直接 return，不初始化 SparkleUpdater

## 验证
1. dotnet build 0错误
2. dotnet test 无新增失败
3. dotnet run → 打开混音台 → 扫描VST → 选插件 → 自动弹出插件编辑器窗口
4. 播放 → 听效果 → 确认VST生效
5. Mute/Solo 混音台和主界面一致
6. 保存为.ustxp → 重新打开 → 混音台状态恢复
7. 偏好设置 → VST路径表格可选可删
8. 通用更新不再触发
