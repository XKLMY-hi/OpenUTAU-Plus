---
name: metronome-piano-port
description: 上游定向移植进度 — 节拍器已完成验收；钢琴窗批次 A 进行至 A7，A8/A9 待做（2026-09-16）
metadata:
  node_type: memory
  type: project
---

# 上游定向移植：节拍器 + 钢琴窗增强

计划文档：`.opencode/plans/upstream-piano-metronome-port.md`（批次表；A1-A7 已完成）

## 已完成（全部提交在 plus-develop）

### 节拍器（762e8be1）✅ 用户实机验收通过
- click/每小节重音/播放中变速拍号跟随正常；导出无残留
- **架构隔离已被问及并解答**：节拍器只在 PlaybackOverlay（播放输出层，PlaybackManager.cs:232）注入，导出走 RenderEngine 产物不经过 overlay；VST 在渲染链内部（渲染产物之前）→ 双向隔离。已知行为：主推子静音时 click 仍响；频谱不含 click
- 上游参考源码：`E:\home\Temp\metro\`（可能被清，可从 23779f5d 重新提取）
- 关键适配：MetronomeEngine 删 const SampleRate/Channels 改 AudioSettings；PlaybackOverlay 替代上游 PlaybackMix（扣除 master.Waited 换算全局位置）

### 字体崩溃修复（8e0041bf + 22de6533）✅ 本机已实测消除
- 22de6533：AccessText / ToolTip / ContextMenu / MenuFlyoutPresenter / FlyoutPresenter 显式 PlusFontFamily；Window 字体改 PlusFontFamily；Program.cs UnhandledException 加 [FontDiag] 视觉树 $Default 控件 dump
- 崩溃背景：$Default（FontFamily.Default）在本机 Skia 解析失败 → glyphTypeface 异常；TextBlock 子类不吃 TextBlock 选择器、PopupRoot 不继承 Window 字体是两条残留路径

### 钢琴窗批次 A（进行中）

| 序 | 提交 | 本地提交 |
|---|---|---|
| A1 #2230 关闭按钮/双击隐藏/分离窗开关 | a66fc11d | b65d1f8f |
| A2 #2266 ctrl 拖移 | afbdedf8 | d7ac5f82（合并） |
| A3 #2377 快捷键 | 64fedd61 | d7ac5f82（合并） |
| A4 #2196 resizeNeighbor | d5a5d2c0 | d7ac5f82（合并） |
| A5 #2332 音高节点工具 | 7a67e052 | d7ac5f82（合并） |
| A6 #2362 发音提示批量编辑 | a14212cd | eb5ba981 |
| A7 悬停辉光+播放高亮 | 68a3bd97 | c9a04174 |

**顺序教训**：A3 快捷键修复依赖 A5 的工具索引（PitchPointTool=40 插入后 Shift 映射才成立），已按 A2→A4→A5→A3 适配应用。

**A7 适配点**：Plus RenderNoteBody 配色与上游不同（Error→Accent2Semi 而非 Accent3）；`.OfType<UVoicePart>()` 在 Plus nullable 下不支持，改 `.Where(p => p != null)`（NotesCanvas 需 using System.Reactive.Linq）。

## 剩余工作（恢复时按序）

1. **A8 bed088a4 播放音符弹跳**（分析已完成，代码未写；`git show bed088a4` 取 diff）：
   - Preferences + `ShowPlaybackNoteBounce = false`
   - NotesCanvas：DirectProperty + 字段 `showPlaybackNoteBounce/activeBounceElapsed` + 常量 `PlaybackNoteBounceDuration=0.25f / PlaybackNoteBounceHeight=12.0`；OnPropertyChanged 两处条件加 `|| ShowPlaybackNoteBounce`；UpdatePlaybackHighlight（target 条件、activeBounceElapsed 重置、bouncing 块、needed）；`GetPlaybackBounceOffset(note)`（0.25s 半正弦、高度 min(12, TrackHeight*0.4)）；RenderNoteBody 在 `size` 调整后 `leftTop += GetPlaybackBounceOffset(note);`
   - PianoRoll.axaml 绑定 `ShowPlaybackNoteBounce="{Binding NotesViewModel.ShowPlaybackNoteBounce}"`
   - NotesViewModel（属性/初始化/case "PlaybackNoteBounce"）/ PreferencesViewModel（同）/ PreferencesDialog（ToggleSwitch）/ Strings ×2（`prefs.appearance.playbacknotebounce` 中译"播放时音符弹跳"）
2. **A9 fd4fc950 辉光改进**（未分析；`git show fd4fc950`）
3. **81637a33 #2416 钢琴窗显示范围高亮**（上游 2026-09-16 推送，最晚并入）
4. **批次 B**：B1 0c934958 Alt 拖拽复制（NoteEditStates 47 行）；B2 2645b69a 曲线编辑扩展（613 行，最大项，需 A 批次落地后做）
5. **暂缓**：ef037d8e 实时波形 / 2a1c8d5f 实时曲线刷新 / 984e53d5 DiffSinger 局部重绘（依赖渲染重构，随全量合并）
6. 每个特性完成后：构建 0 错误 + 测试全绿 → 用户实机预览（UI 不可自动交互）

## 环境/工具注意（2026-09-16 实测）

- **构建**：`export DOTNET_ROOT=; dotnet build OpenUtau.sln --no-restore -m:1`
  - ⚠️ 先关掉运行中的 OpenUtau.exe（`taskkill //PID <pid> //F`），否则 dll 锁定 MSB3027/MSB3021
  - ⚠️ 构建异常后可能产出双份 avares 损坏 dll（`Key: /Assets/Icons.axaml` 重复报错）→ 删 `OpenUtau/obj/Debug/net8.0-windows` 重建
- **测试**：`export DOTNET_ROOT="E:\tools\dotnet"; export DOTNET_ROLL_FORWARD=Major; dotnet test OpenUtau.Test --no-build` → 基线 **284/284**（若单次失败先重跑，flaky；连续失败再查）
- **启动**：`DOTNET_ROOT= ./OpenUtau/bin/Debug/net8.0-windows/OpenUtau.exe > 输出文件 2>&1 &`（后台；清 DOTNET_ROOT 用系统 8.0.11）
- 上游提交均在本地 `upstream/master` 可 `git show`；提取参考 diff 已不需要网络

**How to apply:** 恢复会话先读本文件 + 计划文档；从"剩余工作"第 1 项（A8）继续。
