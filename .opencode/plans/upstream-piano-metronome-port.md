# 上游定向移植计划：节拍器 + 钢琴窗增强（2026-09-15）

## 进度快照（2026-09-18）

- ✅ **目标一 节拍器**：已提交 **762e8be1**，用户实机验收通过（click/重音/变速跟随/导出无残留）；VST/导出结构隔离已确认
- ✅ **字体崩溃加固**：22de6533（$Default 全路径令牌化 + [FontDiag] 诊断）
- 🚧 **目标二 钢琴窗批次 A ✅ 全部完成**：A1 b65d1f8f · A2+A3+A4+A5 d7ac5f82 · A6 eb5ba981 · A7 c9a04174 · A8 2954a723 · A9 bdcaf9b4 · **A10 ff2836ec**（A8-A10 待用户实机验收）
- ⏳ 下一步 **批次 B**：B1 0c934958 Alt 拖拽复制 → B2 2645b69a 曲线编辑扩展
- 🧪 环境重启后构建链重建（G 盘/系统 SDK/离线 NuGet），并修掉 RenderGate 并发用例对全局零值的硬断言（c8532db6）；测试基线保持 **284/284**
- ⏳ 批次 B（B1/B2）待 A 批次完成后做

## Context

上游 `upstream/master` 自 2026-08-01 合并点（29e0e16d）后新增 **132 提交**（Newtonsoft→System.Text.Json 迁移、frozen slot 音频重构、渲染管线重构、UI 改动）。`merge-tree` dry-run 显示 **42 处冲突**，其中音频/渲染核心是两套独立架构的碰撞——全量合并需要架构决策，暂缓。

用户指定**优先定向移植两类特性**，不等待全量合并：

1. **节拍器**（上游 #2341）
2. **钢琴窗增强**（上游 #2230/#2266/#2377/#2196/#2332/#2362/#2267/#2393 等）

移植基线：`plus-develop` @ 8e0041bf（字体修复后）。移植源：已 fetch 的 `upstream/master`。

---

## 目标一：节拍器（#2341，提交 23779f5d）

**架构独立性结论**：节拍器合并于上游 frozen slot 重构（6196917f）**之前**，重构只改了集成层（PlaybackManager.cs），`Metronome*.cs` 本体未被后续修改 → 可移植到 Plus 自有播放管线。

**上游改动：10 文件 +487/-29**

| 项 | 内容 |
|---|---|
| 新增 `OpenUtau.Core/SignalChain/MetronomeScheduler.cs` | 55 行，纯逻辑：按 TimeAxis 走小节/拍，维持下一个拍点的 tick+ms |
| 新增 `OpenUtau.Core/SignalChain/MetronomeEngine.cs` | 130 行，`ISignalSource`：Mix 内按采样区间发出拍点（支持 buffer 内偏移），重拍高 880Hz |
| `PlaybackManager.cs` | `PlaybackMix` 叠加层（master + metronome，暴露 MasterExhausted）；`Metronome` 属性；`PlayMetronomeClick()` 预览；`GetMetronomeGain()`；Start/Stop/UpdateSchedule 生命周期钩子；`ToneGenerator` 扩展（`SetGain`、`StartTone(freq,attack,release,startSampleOffset)`、`StartTones`；`SineGenerator` 加 `SetGain`/`startSampleOffset`） |
| `Preferences.cs` | 4 个字段：`Metronome`、`MetronomeVolume`、`MetronomeHighFrequency`、`MetronomeLowFrequency` |
| UI | `PlaybackViewModel`（开关）、`MainWindow.axaml`（运输栏按钮）、`PreferencesDialog.axaml(.cs)` + `PreferencesViewModel`（音量/频率/预览）、`Strings.axaml`（4 键） |

**Plus 适配点（风险）**：
- Plus `PlaybackManager` 已自主重写（两批播放/RenderGate/ExportSession/VST 命令化）——`PlaybackMix` 与生命周期钩子需**人工设计集成点**，不能照抄
- Plus `ToneGenerator` 为 Plus 版（钢琴键试音改造），需对照合并出 metronome 所需的 API 扩展
- Plus `MainWindow.axaml` 与上游差异极大（SukiUI + 自绘）——按钮手工加
- 导出路径注意：节拍器仅播放期生效，不影响 ExportSession 输出

**验收**：构建 0 错误；测试全绿；实机：播放时听到每拍 click、每小节首拍重音；改速度/拍号后点跟随；播放/暂停/跳转无残留音。

---

## 目标二：钢琴窗增强

### 批次 A：旧架构可移植（按时间序逐个 pick + 适配）

| 序 | 提交 | 内容 | 状态 |
|---|---|---|---|
| A1 | a66fc11d | #2230 钢琴窗 UI：工具栏关闭键、双击关闭、分离窗 UX | ✅ b65d1f8f |
| A2 | afbdedf8 | #2266 ctrl 拖移修复 | ✅ d7ac5f82 |
| A3 | 64fedd61 | #2377 EditTools 快捷键修复 | ✅ d7ac5f82（须在 A5 后应用） |
| A4 | d5a5d2c0 | #2196 resizeNeighbor 初始化逻辑 | ✅ d7ac5f82 |
| A5 | 7a67e052 | #2332 PitchPointTool | ✅ d7ac5f82 |
| A6 | a14212cd | #2362 歌词音素提示 | ✅ eb5ba981 |
| A7 | 68a3bd97 | 悬停辉光 + 播放音符高亮 | ✅ c9a04174 |
| A8 | bed088a4 | 播放音符弹跳（默认关） | ✅ 2954a723（待用户验收） |
| A9 | fd4fc950 | 辉光改进（#2360） | ✅ bdcaf9b4（待用户验收） |
| A10 | 81637a33 | #2416 钢琴窗显示范围内高亮 | ✅ ff2836ec（待用户验收） |

### 批次 B：需适配的上游中间态

| 序 | 提交 | 内容 | 说明 |
|---|---|---|---|
| B1 | 0c934958 | #2267 Alt 拖拽复制音符（NoteEditStates 47 行） | 依赖新架构提交祖先，但改动本体为编辑逻辑，可适配 |
| B2 | 2645b69a | #2393 曲线编辑扩展（UCurve/PianoRoll/CurveViewModel/NoteEditStates，613 行） | 最大单项；无新架构类型引用（已 grep 验证）；依赖 A 批次先落地以减少冲突 |

### 暂缓（依赖渲染重构，随全量合并）

- ef037d8e #2119 实时波形（新 IRenderer）
- 2a1c8d5f #2175 实时曲线刷新（RealCurveUpdater）
- 984e53d5 #2183 DiffSinger 局部重绘（新 IRenderer）

---

## 工作约定

- 每个特性/提交 = 一个原子提交，Conventional Commits 中文风格（同仓库惯例）
- 冲突适配时保留 Plus 侧主题/字体令牌（参考 8e0041bf 字体可移植化修复，禁止裸 `monospace`）
- 每步：`dotnet build OpenUtau.sln --no-restore -m:1` 0 错误 → `dotnet test OpenUtau.Test --no-build` 全绿 → 用户实机预览确认（UI 不能自动交互）
- 环境备注：包缓存已还原至 `E:\home\.nuget\packages`（离线可构建）；运行 app 时需清 `DOTNET_ROOT`（便携 SDK 9 无 net8 运行时，系统 `C:\Program Files\dotnet` 有 8.0.11）；测试需完整权限

## 后置：全量合并时的顺序

1. 先解非音频冲突（工程文件/测试/UI）
2. JSON 迁移收尾（Plus 侧 18 个 Newtonsoft 文件迁到 System.Text.Json）
3. 音频/渲染架构统一（上游 frozen slot 为基底，Plus 特性重放）——届时再决策
