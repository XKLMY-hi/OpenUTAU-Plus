---
name: metronome-piano-port
description: 上游定向移植进度 — 节拍器与钢琴窗增强（2026-09-15 开始，节拍器分析完成待写码）
metadata:
  node_type: memory
  type: project
---

# 上游定向移植：节拍器 + 钢琴窗增强

计划文档：`.opencode/plans/upstream-piano-metronome-port.md`（完整批次表与验证约定）

## 背景结论（2026-09-15 评估）

- upstream/master 领先 plus-develop **132 提交**；全量合并 dry-run（`git merge-tree`）**42 冲突**
- 三大碰撞区：①音频/渲染双架构碰撞（Plus 音频管线重构 vs 上游 frozen slot planner，`WaveSource.cs` modify/delete）②Newtonsoft→System.Text.Json 迁移（Plus 侧仍有 18 文件用 Newtonsoft）③UI 层（SukiUI+12 vs 上游 UI 改动）
- **决策：先定向移植节拍器+钢琴窗增强，全量合并后置**

## 已完成

- **字体可移植化修复已提交 8e0041bf**：裸 `monospace` 与 `$Default` → Plus 字体令牌（本机 Skia 对两者均解析失败，渲染即崩）；10 文件
- **构建环境修复**：176 个 NuGet 包已还原到 `E:\home\.nuget\packages`（此后离线 `dotnet build` 可用）
  - 运行 app 需清 `DOTNET_ROOT`（便携 SDK 9 无 net8 运行时；系统 `C:\Program Files\dotnet` 有 8.0.11）
  - `dotnet test` 需完整权限（testhost 句柄要求）

## 节拍器移植（上游 23779f5d / #2341）— 分析完成，代码未写

上游参考文件已提取到 `E:\home\Temp\metro\`（MetronomeScheduler.cs / MetronomeEngine.cs / pref.diff / playback.diff / ui.diff）

**可直接移植（已验证依赖）**：
- `MetronomeScheduler.cs` 逐字用（`TimeAxis` 在 `OpenUtau.Core.Util` ✓；`BpmCommand` 等 6 个命令类型存在 ✓；`playPosTick` 为 DocManager 公开字段 ✓）
- `MetronomeEngine.cs` 适配点：删掉 `const SampleRate/Channels`，改用 `ISignalSource` 默认成员（= `AudioSettings`，Plus 全局音频事实来源）

**Plus 集成设计（与上游不同，原因已验证）**：
- Plus `RenderEngine.RenderProject` 返回 `MasterAdapter`（ISampleProvider），上游 `PlaybackMix : ISignalSource` 无法照搬
- 方案：新增 **ISampleProvider 叠加层**（暂名 `PlaybackOverlay`）：包住 `masterMix`；`Read` 先调 `master.Read`，再按 `readSamples - masterMix.Waited` 得到时间轴位置调用 `metronomeEngine.Mix(position, ...)`，**返回 master 的 n**（n=0 时输出自然停止 = 保持现状，无需上游 `MasterExhausted`/`playbackEndTick` 逻辑）
- `StartPlayback`：加 `metronomeEngine.StartPlayback(project.timeAxis, StartTick)`，`InitOutput(overlay)` 替代 `InitOutput(masterMix)`
- `Play()` 暂停恢复分支 + `StopPlayback`/`PausePlayback`：加 metronome StartPlayback/Stop（照上游）
- `OnNext`：加 `BpmCommand/TimeSignatureCommand/Add·DelTempoChange/Add·DelTimeSig` → `metronomeEngine.UpdateSchedule`
- 预览 `PlayMetronomeClick`：照上游（NAudio `MixingSampleProvider` + `OffsetSampleProvider` + `SineGenerator(freq, gain, 5, 80)`，延迟 0/300ms，Take 120ms）；Plus 侧走 `InitOutput`
- **ToneGenerator/SineGenerator 扩展**（PlaybackManager.cs 内，上游 playback.diff 行 44-160 为准）：
  - `SineGenerator`：+`startSampleOffset`（Read 内 `i < startSampleOffset ? 0 : GetNextSample()`，读后递减）、+`SetGain`、+5 参构造
  - `ToneGenerator`：`gain` 去 readonly、+`SetGain`（含活动/非活动 generator）、+`StartTone(freq,attack,release,offset)`、+`StartTones`、+`EndTones`；`StartTone/EndTone/EndAllTones` 字典操作移入锁内

**Preferences.cs 插入点**（Plus 文件）：
- L177 `DiffSingerLangCodeHide` 后：`public bool Metronome = false;`
- L194 `PlayPosMarkerMargin` 后：`MetronomeVolume=60` / `MetronomeHighFrequency=2200` / `MetronomeLowFrequency=1320`

**UI 插入点（待做）**：
- `PlaybackViewModel`：+`Metronome` bool 属性（照上游 ui.diff）
- `MainWindow.axaml`：运输栏在 ~L250（`Classes="playBtn"`，Plus 已改版，上游 ui.diff 的按钮布局不可照搬，需手工加 ToggleButton）
- `PreferencesDialog.axaml` 回放节 + `.axaml.cs` 右键复位 handler（`OnMetronomeSliderPointerPressed`）+ `PreferencesViewModel` 3 属性/订阅/`TestMetronome`/`ResetMetronome*`
- `Strings.axaml` + `Strings.zh-CN.axaml`：`prefs.playback.metronome` / `.volume` / `.highfrequency` / `.lowfrequency`

## 下一步（恢复时按序）

1. 新建 `MetronomeScheduler.cs` + `MetronomeEngine.cs`
2. Preferences 4 字段
3. ToneGenerator/SineGenerator 扩展
4. PlaybackManager 集成（overlay + 生命周期 + OnNext）
5. UI 五处
6. 构建 0 错误 + 测试全绿 → 用户实机验证（click/重音/变速跟随）
7. 钢琴窗批次（计划文档批次 A/B）

**How to apply:** 恢复会话时先读本文件 + 计划文档，从"下一步"第 1 项继续。
