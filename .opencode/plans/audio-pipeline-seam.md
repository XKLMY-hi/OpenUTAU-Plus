# 音频管线接缝（合成层 / 音频消费层解耦）— 2026-09-18

提交：`88944390`（refactor(audio)）。测试基线 294 → **308** 全绿。

## 为什么做

上游把音频管线整体换成了 frozen slot planner + 渲染视图 + 优先级调度（+2000 行新文件），
我们要能"整根换掉合成实现"而不连坐运输/混音/导出层。本次只做**最小解耦**：把合成层的
对外契约显式化 + 把不属于合成的东西移出去，行为零变化。

## 接缝定义（改后的契约）

**合成层对外只有 `RenderEngine` 的静态门面**（`Render/RenderEngine.cs` 末尾）：

| 成员 | 用途 | 返回 |
|---|---|---|
| `PreRender(project)` | 后台预热全曲乐句 | void |
| `RenderProject(project, uiScheduler, ref cts, startTick, endTick, trackNo)` | 播放渲染（两批策略在内部） | `Tuple<MasterAdapter, List<Fader>>?` |
| `RenderMixdown(project, uiScheduler, ref cts, wait, applyMixFx, startTick, endTick, trackNo)` | 离线混音（播放/导出共用） | `Tuple<WaveMix, List<Fader>>` |
| `RenderTracks(project, uiScheduler, ref cts, startTick, endTick)` | 分轨导出 | `List<WaveMix>` |
| `InvalidatePhraseCache()` | 整体失效乐句缓存 | void |
| `ReleaseSourceTemp()` | 缓存目录清场 | void |

**运输/混音/导出层允许跨越接缝的类型只有两类**：
- 音频数据：`ISignalSource` / `WaveMix`
- 混音与输出形态：`Fader` / `MasterAdapter`

**禁止**：`PlaybackManager` / `ExportSession` 构造 `RenderEngine`、持有或暴露
`PhraseRenderCache`、在字段/属性签名里出现 `RenderPhrase` / `RenderNote` / `RenderPhone` /
`RenderPartRequest` / `WaveSource` / `RenderResult` / `RenderPitchResult`。

以上由 `OpenUtau.Test/App/AudioSeamContractTests.cs`（14 条反射契约测试）锁定。

## 本次改动清单

1. `RenderEngine`：乐句缓存改为合成层私有静态字段 `phraseCache`；新增上述静态门面；
   `PreRenderProject` 的共享 CTS 也收回合成层（原由 `PlaybackManager.renderCancellation` 传入）。
2. `PlaybackManager`：删除 `public readonly PhraseRenderCache PhraseCache`；三处 `new RenderEngine(...)`
   改调门面；工程切换 `PhraseCache.Clear()` → `RenderEngine.InvalidatePhraseCache()`。
3. `ExportSession`：构造签名去掉 `cache` 参数（`RenderWindow` 调用点同步）——导出层不再知道缓存存在。
4. `RenderGate`：`OpenUtau.Core.Vst` → **`OpenUtau.Audio`**（与 `IAudioOutput` /
   `CallbackTrackedSampleProvider` 同层）。语义：渲染/导出**消费段**在飞计数，VST 延迟销毁只是它的
   一个消费者；合成层与播放层都引用但不拥有。
5. 新增 `AudioSeamContractTests`。

## 已知的下一层耦合（本次**故意**没动）

- `MasterAdapter.Waited` / `IsWaiting`：由合成层的 `WaveSource` 未就绪产生、被运输层
  （播放头位置换算 `PlaybackManager.cs:598`、节拍器对时 `PlaybackOverlay.Read`）消费。
  上游新架构没有这个概念 → **换合成实现前必须把等待时长的统计搬到运输层**（约 20 行）。
- `VstPluginManager.TryFlushAllPendingDispose()` 依赖"换源安全点"（`StartPlayback`）与
  RenderGate == 0 —— 这是 Plus 独有需求（上游无播放中换源、无 VST 宿主），换合成实现时要保留。

## 下一步（评估用）

1. 用同一工程对新旧合成实现做 A/B 音频比对（导出逐样本比较），验证接缝真能整根替换。
2. 若通过，正式引入上游渲染实现（`MixPlanner`/`SampleSlot`/`RenderView`/`RenderPriority`），
   保留本门面签名不变；届时 `Waited` 迁移与 VST 安全点需按上节处理。
3. 四类忠实行为回归清单：播放中 seek 不卡顿、三条导出路径一致、VST 延迟销毁、节拍器与频谱。
