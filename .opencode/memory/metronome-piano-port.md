---
name: metronome-piano-port
description: 上游定向移植进度 — 节拍器已完成验收；钢琴窗批次 A 进行至 A8，A9/A10/批次 B 待做（2026-09-18）
metadata:
  node_type: memory
  type: project
---

# 上游定向移植：节拍器 + 钢琴窗增强

计划文档：`.opencode/plans/upstream-piano-metronome-port.md`（批次表；A1-A8 已完成）

## 已完成（全部提交在 plus-develop）

### 节拍器（762e8be1）✅ 用户实机验收通过
- click/每小节重音/播放中变速拍号跟随正常；导出无残留
- **架构隔离已被问及并解答**：节拍器只在 PlaybackOverlay（播放输出层，PlaybackManager.cs:232）注入，导出走 RenderEngine 产物不经过 overlay；VST 在渲染链内部（渲染产物之前）→ 双向隔离。已知行为：主推子静音时 click 仍响；频谱不含 click
- 上游参考源码：`E:\home\Temp\metro\`（可能被清，可从 23779f5d 重新提取）
- 关键适配：MetronomeEngine 删 const SampleRate/Channels 改 AudioSettings；PlaybackOverlay 替代上游 PlaybackMix（扣除 master.Waited 换算全局位置）

### 字体崩溃修复（8e0041bf + 22de6533）✅ 本机已实测消除
- 22de6533：AccessText / ToolTip / ContextMenu / MenuFlyoutPresenter / FlyoutPresenter 显式 PlusFontFamily；Window 字体改 PlusFontFamily；Program.cs UnhandledException 加 [FontDiag] 视觉树 $Default 控件 dump
- 崩溃背景：$Default（FontFamily.Default）在本机 Skia 解析失败 → glyphTypeface 异常；TextBlock 子类不吃 TextBlock 选择器、PopupRoot 不继承 Window 字体是两条残留路径

### 播放音符弹跳（A8，2954a723）⏳ 待用户实机验收
- 行为：播放头进入音符时该音符上跳 0.25s（半正弦弧，高度 min(12px, 轨道高 40%)），随后落回；偏好「外观 → 播放时音符弹跳」默认**关**
- 与「播放时高亮」相互独立：任一开启即维持 30fps 播放帧循环（`UpdatePlaybackHighlight` 的 needed 条件），两者都关时行为与 A7 前完全一致
- 弹跳偏移在 `RenderNoteBody` 的 size 调整之后叠加，故只动绘制位置、不改命中/编辑几何

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
| A8 播放音符弹跳 | bed088a4 | 2954a723 |

**顺序教训**：A3 快捷键修复依赖 A5 的工具索引（PitchPointTool=40 插入后 Shift 映射才成立），已按 A2→A4→A5→A3 适配应用。

**A7 适配点**：Plus RenderNoteBody 配色与上游不同（Error→Accent2Semi 而非 Accent3）；`.OfType<UVoicePart>()` 在 Plus nullable 下不支持，改 `.Where(p => p != null)`（NotesCanvas 需 using System.Reactive.Linq）。

**A8 适配点**：与上游 diff 逐行等价（7 文件 +55/-4），仅 PreferencesDialog 行距沿用 Plus 的 `Margin="0,4,0,0"`；`Vector` 由 `using Avalonia;` 提供，无需新增 using。

## 剩余工作（恢复时按序）

1. **A9 fd4fc950 辉光改进**（未分析；`git show fd4fc950`），接着 A10 81637a33 #2416 钢琴窗显示范围高亮
2. **批次 B**：B1 0c934958 Alt 拖拽复制（NoteEditStates 47 行）；B2 2645b69a 曲线编辑扩展（613 行，最大项，需 A 批次落地后做）
3. **暂缓**：ef037d8e 实时波形 / 2a1c8d5f 实时曲线刷新 / 984e53d5 DiffSinger 局部重绘（依赖渲染重构，随全量合并）
4. 每个特性完成后：构建 0 错误 + 测试全绿 → 用户实机预览（UI 不可自动交互）

## 环境/工具注意（2026-09-18 重测；旧命令已失效）

⚠️ 上次记录的 `E:\tools\dotnet` / `E:\home\.nuget\packages` 已不存在——命令换成系统 dotnet，详见 [[env-refresh-2026-09-g-drive]]：

```powershell
dotnet restore OpenUtau.sln -m:1 -p:TreatWarningsAsErrors=false --ignore-failed-sources
dotnet restore VstProbe\VstProbe.csproj -m:1 -p:RuntimeIdentifiers= -p:TreatWarningsAsErrors=false --ignore-failed-sources
dotnet build OpenUtau.sln --no-restore -m:1 -p:RuntimeIdentifiers= -p:UsedAvaloniaProducts=
dotnet test OpenUtau.Test\OpenUtau.Test.csproj --no-build      # 基线 284/284（约 2-4 分钟）
.\OpenUtau\bin\Debug\net8.0-windows\OpenUtau.exe
```

- ⚠️ 构建前先关掉运行中的 OpenUtau.exe，否则 dll 锁定 MSB3027/MSB3021
- ⚠️ 构建异常后可能产出双份 avares 损坏 dll（`Key: /Assets/Icons.axaml` 重复报错）→ 删 `OpenUtau/obj` 重建
- ⚠️ `obj/**` 历史产物可能整体只读（Access denied 且 ACL 正常）→ 整目录删除重建
- ⚠️ 编辑源码工具会吃掉 UTF-8 BOM（.editorconfig 对 *.cs 要求 utf-8-bom）→ 提交前用 `UTF8Encoding($true)` 写回，避免首行噪声 diff
- 上游提交均在本地 `upstream/master` 可 `git show`；提取参考 diff 不需要网络

**How to apply:** 恢复会话先读本文件 + 计划文档；从"剩余工作"第 1 项（A9）继续。
