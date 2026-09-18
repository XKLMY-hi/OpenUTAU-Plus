# 上游音频管线 & 歌声合成变动盘点（2026-09-18）

基线：`29e0e16d`（Plus 上次与上游合并点 = 2026-08-01 前的 master）→ `upstream/master` @ `83e02c7e`
**上游领先 153 提交；Plus 自分叉以来本地 248 提交**。上游节奏约 5-7 提交/天（9-10 至 9-18 共 57 提交）。

## 一、音频管线：整体已被重写（不是增量修改）

| 阶段 | 提交 | 内容 | 新增文件 |
|---|---|---|---|
| 架构替换 | 6196917f | **frozen slot planner** 取代 WaveSource 播放传输：不可变样本槽 + 计划器，删 `WaveSource.cs`(-54) | `SignalChain/SampleSlot.cs` `SignalChain/MixPlanner.cs` `Util/Frozen.cs` |
| 渲染只读视图 | f773f377 / 7c68a087 / 17bf25e7 | 文档驱动波形读取、渲染视图（`RenderView`/`RenderProjection`/`PhraseLayout`）、乐句构建移出 UI 线程（`Pipeline/Snapshots` `PhraseSource` `PhraseSourceBuilder` + `ThreadGuard`） | 7 个新文件，共 ~1700 行 |
| 调度 | 1d115473 | 渲染优先级调度（`RenderPriority`） |  |
| 实时刷新 | ef037d8e / 2a1c8d5f | 实时渲染波形（`WaveformRefresh`）、真实曲线刷新（`RealCurveUpdater`） |  |
| 播放 | eaee391a / e34dbb43 / 832aea2c | 显式循环开关、首次播放立即被停的修复、波形通知线程归类 |  |
| 后端 | 60a6c197 | **SDL3 音频后端**（新文件 `Audio/SDL3AudioOutput.cs` +270，csproj/偏好/启动窗口） |  |
| 乐句合并 | f32e4ab5 / 38401382 / 4696de48 | 渲染器 padding API 合并重叠乐句；乐句间空隙按静音渲染；**合并乐句空隙的 pitch 尖峰修复** |  |

关键差异：上游走 **WaveMix/frozen slot 重建**；Plus 自研 **两批播放 + RenderGate + ExportSession**（导出直接写 RenderEngine 产物、不经播放 overlay）。两侧**同类但不同实现**，所以全量合并 42 处冲突主要来自这里。

## 二、歌声合成：小 API 改动 + 大量音素化器重写 + DiffSinger 重构

### 2.1 音素化 API（4 文件，+72/-34，小）
- `Api/Phonemizer.cs`：`OnAsyncInitStarted/Finished` 标 Obsolete 且改 **no-op**（约定 SetUp/Process/CleanUp 单后台线程同步加载）；父级表达式取值方法加守卫与日志
- `Api/PhonemizerFactory.cs`：`Dictionary` → **`ConcurrentDictionary`**（工厂缓存线程安全）
- `Api/PhonemizerRunner.cs`：+34（runner 自己上报初始化进度）
- 新增 `Api/IG2pSymbols.cs`（9 行接口，给 G2P 工具用）

### 2.2 Plugin.Builtin 音素化器（83 文件，+10552/-5313）
大重写：`SyllableBasedPhonemizer`(+1295/-…) · `ENtoJAPhonemizer`(+1331/-397) · `EnglishCpVPhonemizer`(813) · `FilipinoPhonemizer`(-980) · `EnXSampaPhonemizer`(316) · `EnglishVCCVPhonemizer`(415) · `ChineseVCVPhonemizer`(+242 樗儿支持) · 新增 `en2ja.template.yaml`(+1705) / `fil2ja.template.yaml`(+1663)
小修：KoreanCV kocvS 判空 · Presamp 字典/编码 · UstFlagParser · Fixed "c+v" · Oto 输出 0 而非空白 · 旧版校验 · 各语言 VCCV/CVVC 的守卫生成

### 2.3 DiffSinger（15 文件 / +~1200）
`ba5b1659` variance retake（hard compose，+519） · `984e53d5` **pitch local retaking**（+411） · `5afde866` 实时 pitch 生成（+475） · `2a1c8d5f` RealCurveScheduler · `e3db1f9d` 取消 LocalRetaking 开关（默认总是局部 retake） · `f39fc706` 模型创建/释放按歌手串行化 · `6c322550` G2P 修复 · `df0555d8` Lang ID 修复

### 2.4 其它合成侧（无本地对应物）
XSY 交叉合成（3eb9dd26，经典渲染 +423，含新 DSP）· Voicevox slur（8dec0f51，+313）· GAME 转写的 GGML 后端（341e4ef4，+884）

### 2.5 同时期也有一批无关音频的通用改动
Newtonsoft→System.Text.Json 迁移（535e6857）· 内存泄漏修复（bfb01058）· Ctrl+LMB 音符选择修复（4941bf21）· 隐藏失败可视化（fe0894d3）· Toast（66e31073）

## 三、可独立移植（不动架构）

**A 类 — 已验证存在的风险/修复**
1. `9138af6e` Worldline 渲染缓存文件按路径串行化（12 行）——Plus 同样有 Worldline 缓存文件与并行渲染
2. `e34dbb43` 首次播放立即被停（PlaybackManager 1 行 + MainWindow 1 行）
3. `2c283d2b` 父级表达式取值守卫：Plus `Phonemizer.GetParentVoiceColor()` 仍是对 `track.VoiceColorExp.options[(int)trackCLR.CustomDefaultValue]` 的**无边界检查**索引
4. `7684d706` PhonemizerFactory 工厂缓存线程安全（Plus 仍是普通 Dictionary，`factories[type] = factory` 无锁）
5. `7a083786` ClassicSinger FreeMemory 状态未清（2 文件 3 行）
6. `3602d9a1` KoreanCV kocvS 判空（4 行）
7. `a46de4e0` UstFlagParser 修复
8. `57567b5b` / `56eafb70` 旧版校验与 Oto 空白输出
9. `49daf1ed` / `a8ddc510` 音素化 API 简化（需与 runner/调用点一起看）

**B 类 — 成本中等，需适配**
- `eaee391a` 播放循环开关（PlaybackManager + MasterAdapter，Plus 结构不同但概念对应）
- `60a6c197` SDL3 音频后端（新文件独立，但 Plus 的音频输出选择/偏好接线要手工加）

## 四、当前不可移植（依赖新架构）

- frozen slot / MixPlanner / SampleSlot / RenderView / RenderProjection / PhraseSource（6196917f、7c68a087、17bf25e7）
- 渲染优先级、实时波形、真实曲线刷新（1d115473、ef037d8e、2a1c8d5f）
- 乐句合并 padding 体系与 **pitch 尖峰修复**（f32e4ab5、38401382、4696de48）——Plus 是逐乐句独立渲染，无合并乐句，该 bug 在 Plus 上不存在
- XSY（3eb9dd26，挂在经典渲染新路径）
- DiffSinger variance retake / local retaking / live pitch（需新 IRenderer/乐句快照）

## 五、建议（待用户决策）

1. **先做 A 类低风险修复**（约 9 项，单提交或两三个提交，纯修补、不动架构）：其中 #3 边界检查、#4 工厂并发、#1 缓存串行化是真 bug 风险
2. **XSY 值得单独评估**（经典渲染的交叉合成是功能项，Plus 用户可感知），但需判断是否要引入其依赖的渲染路径
3. 大项（DiffSinger 系列 / 音素化器重写 / 音频架构统一）留给全量合并阶段，按计划文档"后置"节顺序推进
