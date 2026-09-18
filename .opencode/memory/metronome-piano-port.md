---
name: metronome-piano-port
description: 上游定向移植进度 — 节拍器已验收；钢琴窗批次 A（A1-A10）与批次 B（B1/B2）已完成待验收（2026-09-18）
metadata:
  node_type: memory
  type: project
---

# 上游定向移植：节拍器 + 钢琴窗增强

计划文档：`.opencode/plans/upstream-piano-metronome-port.md`（批次表；A1-A10 + B1/B2 全部完成）

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

### 悬停光晕改进（A9，bdcaf9b4）⏳ 待用户实机验收
- 光晕颜色改为**音符自身画刷**（选中/错误音符各随其色），不再固定 `ThemeManager.AccentBrush1`
- 悬停命中统一由 `PianoRoll.axaml.cs::NotesCanvasPointerMoved` 的 `HitTestNote` 判定 → 写入 `NotesViewModel.SelectableNote`；仅光标/画笔/橡皮/刻刀工具或按住 Ctrl 时设值
- 左键拖拽中不再残留光晕（`NotesCanvas.OnPointerMoved` 见左键按下即 `SetHoveredNote(null)`）
- 与上游 fd4fc950 逐行等价；Plus 侧无需适配（`HitTestNote` 语义与上游一致）

### 音轨区显示范围高亮（A10，ff2836ec）⏳ 待用户实机验收
- 链路：NotesViewModel 的 `Part` / `TickOffset+ViewportTicks+Bounds` 变化 → `PianoRollOpenPartChangedEvent` / `PianoRollViewportChangedEvent` → TracksViewModel 三个 `[Reactive]` 属性 → MainWindow.axaml 绑定到 PartsCanvas 三个新 DirectProperty → PartControl 经 `canvas.GetObservable(...)` 绑定后重绘
- 绘制：仅对「钢琴窗当前打开的 voice part」在其可见区间画 `Color.FromArgb(28,255,255,255)` 填充 + 2px 白框圆角框；**空片段（无音符）也会画**（上游把 Notes 绘制收进 `notes.Count > 0` 分支，highlight 在其外层）
- 切换片段靠 `PianoRollOpenPartChangedEvent`；载入工程时 `PianoRollOpenPart = null`
- 与上游 81637a33 逐行等价；浅色主题下白框对比度可能偏低（待用户实机反馈再决定是否改用主题令牌）

### 钢琴窗批次 A（✅ 全部完成）

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
| A9 悬停光晕改进 | fd4fc950 | bdcaf9b4 |
| A10 音轨区显示范围高亮 | 81637a33 | ff2836ec |

### 批次 B（✅ 全部完成）

| 序 | 上游提交 | 本地提交 |
|---|---|---|
| B1 Alt 拖拽复制音符 | 0c934958 | b0d001c0 |
| B2 曲线编辑工具扩展 | 2645b69a | d3afb1ed |

**B1 要点**：`NoteMoveEditState` 增加 `duplicate` 参数（首次拖动才 Clone + AddNoteCommand，随后 MoveNoteCommand）；撤销名 `command.note.duplicate`（新增 EN/zh-CN 字符串）。

**B2 要点（本次唯一需要手工解冲突的移植）**：
- 用 `git show 2645b69a > b2.patch` + `git apply -3` 三方合并；3 处冲突全在 Plus 已定型的部分：
  - `Preferences.cs`：Plus 无 `UseWayland` 字段 → 只加 `DefaultSnapCurve`
  - `Strings.axaml`：保留 Plus 自己的 `pianoroll.toggle.expressions/hidepianoroll` 键，插入上游 8 条曲线工具提示
  - `PreferencesViewModel.cs`：Plus 用**内联 WhenAnyValue 订阅**（上游已重构为 `PersistOn`）→ 取 ours 并手加 `DefaultSnapCurve` 订阅
- **编译坑**：上游 `[Reactive] public partial bool DefaultSnapCurve { get; set; }` 是 C# 13 分部属性语法，Plus 用 C# 12（net8.0）+ 旧式 ReactiveUI.Fody 写法 → 必须改回 `[Reactive] public bool ... { get; set; }`
- 偏好 UI：上游那个提交没带 PreferencesDialog 改动 → Plus 手工在「高级」页加 `ToggleSwitch`（`prefs.advanced.defaultsnapcurve`）
- 新增 `OpenUtau.Test/Core/USTx/UCurveTest.cs` 10 个用例（ReplaceRange 语义）→ 测试基线 284 → **294**

**顺序教训**：A3 快捷键修复依赖 A5 的工具索引（PitchPointTool=40 插入后 Shift 映射才成立），已按 A2→A4→A5→A3 适配应用。

**A7 适配点**：Plus RenderNoteBody 配色与上游不同（Error→Accent2Semi 而非 Accent3）；`.OfType<UVoicePart>()` 在 Plus nullable 下不支持，改 `.Where(p => p != null)`（NotesCanvas 需 using System.Reactive.Linq）。

**A8 适配点**：与上游 diff 逐行等价（7 文件 +55/-4），仅 PreferencesDialog 行距沿用 Plus 的 `Margin="0,4,0,0"`；`Vector` 由 `using Avalonia;` 提供，无需新增 using。

## 剩余工作（恢复时按序）

1. **等用户实机验收** A8/A9/A10/B1/B2（清单在 `.opencode/HANDOVER.md`）
2. **验收后可选**：(a) 继续盯上游新的钢琴窗/编辑类提交做定向移植；(b) 转入全量合并评估（upstream 领先 130+ 提交，音频/渲染架构冲突需决策）
3. **暂缓**：ef037d8e 实时波形 / 2a1c8d5f 实时曲线刷新 / 984e53d5 DiffSinger 局部重绘（依赖渲染重构，随全量合并）
4. 每个特性完成后：构建 0 错误 + 测试全绿 → 用户实机预览（UI 不可自动交互）

## 环境/工具注意（2026-09-18 重测；旧命令已失效）

⚠️ 上次记录的 `E:\tools\dotnet` / `E:\home\.nuget\packages` 已不存在——命令换成系统 dotnet，详见 [[env-refresh-2026-09-g-drive]]：

```powershell
dotnet restore OpenUtau.sln -m:1 -p:TreatWarningsAsErrors=false --ignore-failed-sources
dotnet restore VstProbe\VstProbe.csproj -m:1 -p:RuntimeIdentifiers= -p:TreatWarningsAsErrors=false --ignore-failed-sources
dotnet build OpenUtau.sln --no-restore -m:1 -p:RuntimeIdentifiers= -p:UsedAvaloniaProducts=
dotnet test OpenUtau.Test\OpenUtau.Test.csproj --no-build      # 基线 294/294（约 2-4 分钟）
.\OpenUtau\bin\Debug\net8.0-windows\OpenUtau.exe
```

- ⚠️ 构建前先关掉运行中的 OpenUtau.exe，否则 dll 锁定 MSB3027/MSB3021
- ⚠️ 构建异常后可能产出双份 avares 损坏 dll（`Key: /Assets/Icons.axaml` 重复报错）→ 删 `OpenUtau/obj` 重建
- ⚠️ `obj/**` 历史产物可能整体只读（Access denied 且 ACL 正常）→ 整目录删除重建
- ⚠️ 编辑源码工具会吃掉 UTF-8 BOM（.editorconfig 对 *.cs 要求 utf-8-bom）→ 提交前用 `UTF8Encoding($true)` 写回，避免首行噪声 diff
- 上游提交均在本地 `upstream/master` 可 `git show`；提取参考 diff 不需要网络

**How to apply:** 恢复会话先读本文件 + 计划文档；从"剩余工作"第 1 项（批次 B）继续。
