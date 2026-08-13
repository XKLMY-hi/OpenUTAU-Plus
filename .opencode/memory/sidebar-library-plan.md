---
name: sidebar-library-plan
description: 侧栏素材库功能（E4）——已确认 plan 与用户决策，实现待开工
metadata: 
  node_type: memory
  type: project
  originSessionId: 84d7e7cc-b226-48d1-935d-a3e22b84ee0a
---

# 侧栏素材库功能（E4）— 已完成 2026-08-03（db905a81 → 18cf46e4 共 6 提交）

**Plan 文档**：`docs/SIDEBAR-LIBRARY-PLAN.md`（已入库）

## 用户决策（拍板）
1. **歌手卡片**：双击 → 新建轨道添加歌手；拖拽到主编辑器 → 新建轨道添加歌手；无试听
2. **采样库不做**（未来采样器）→ 改**伴奏库**：双击新建轨道添加音频；卡片右侧试听按钮（PlaybackManager.PlayFile）；拖拽新建轨道添加
3. **铭牌只显示 Classic / DiffSinger**（其他引擎不显示）
4. **歌手头像 = 圆角矩形**（非圆形）
5. **伴奏识别性能**：仅枚举文件夹内音频格式文件（wav/mp3/ogg/opus/flac）建卡片（文件名+路径），**不读元数据**；试听/添加时才读取

## 实现要点（探索实证）
- **SidebarViewModel**（新建）：Singers 集合（SingerManager.Inst.SingerGroups 扁平）+ LoadAvatar（复制 SingersViewModel.LoadAvatar，SingersViewModel.cs:316-328）+ RefreshSingers（SearchAllSingers + SingersRefreshedNotification，参照 TrackHeaderViewModel.cs:410-415）；ICmdSubscriber 订阅 SingersRefreshedNotification（MainWindowViewModel.cs:140 注册先例）；伴奏集合 + 扫描器（Directory.GetFiles + 扩展名过滤）
- **新建轨道添加音频**：直接复用 MainWindowViewModel.ImportAudio（MainWindowViewModel.cs:270-288：UWavePart + AddTrackCommand + AddPartCommand + UndoGroup）
- **新建轨道添加歌手**：⚠️ 音素器初始化参照现有建轨路径（TrackHeader 选歌手后 Phonemizer 如何设——实现时验证）
- **拖拽**：卡片 DoDragDrop 自定义格式（"OpenUtau.Singer"/"OpenUtau.Audio"）→ MainWindow.OnDrop 扩展（axaml.cs:1101，先查自定义格式再走原文件逻辑）
- **伴奏目录**：Preferences.SampleSearchPaths 新建 + PathManager.SamplesPath
- 侧栏结构：MainWindow.axaml:269-404（LibraryPanel/SingersPanel/SamplesPanel 占位），样式 sideItem/sideCard/sideHeading/subTab

## 实施步骤（原子提交）
1. SidebarViewModel（歌手/伴奏集合+头像+刷新+通知+扫描）
2. Preferences/PathManager（SampleSearchPaths + SamplesPath）
3. 歌手 UI（圆角矩形头像/铭牌/双击/拖拽）
4. 伴奏 UI（试听按钮/双击/拖拽）
5. MainWindow 对接（拖拽 Drop + 新建轨道命令 + VM 挂载）
6. 文案资源（英+中）

## 实施完成记录
1. E4-1 伴奏库目录设置（Preferences.SampleSearchPaths + PathManager.SamplesPath + 偏好设置 UI）
2. E4-2 SidebarViewModel（歌手/伴奏集合 + 头像 + 刷新 + 通知订阅 + 扫描）
3. E4-3 MainWindowViewModel.AddSingerTrack 命令链（复刻 ApplySingerToTrack）
4. E4-4 歌手 UI（圆角头像/铭牌/双击/拖拽）
5. E4-5 伴奏 UI（试听/双击/拖拽/重扫）
6. E4-6 文案资源 + 收尾

**关键踩坑**（详见 [[sukiui-replacement]]）：Avalonia 12 拖拽 API 改版（DataTransfer/DoDragDropAsync(PointerPressedEventArgs)/DataFormat.CreateInProcessFormat<T>）；空状态用 [Reactive] bool 而非 Count 绑定

## 相关
- 阶段 E 已收官，见 [[sukiui-replacement]]；配色决策见该项目记忆
