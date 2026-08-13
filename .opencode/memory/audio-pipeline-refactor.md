---
name: audio-pipeline-refactor
description: 2026-08-04 音频管线四阶段重构完成——B1 竞态修复、格式显式化、异步渲染+seek 缓存、导出统一+命令接线
metadata: 
  node_type: memory
  type: project
  originSessionId: 84d7e7cc-b226-48d1-935d-a3e22b84ee0a
---

2026-08-04 完成音频管线重构（plus-develop，19 提交，测试全绿）。四个阶段：

**A. VST 安全加固**：B1 竞态根因 = RenderEngine 裸 Flush 在 AudioOutput 换源前执行。修复 = CallbackTrackedSampleProvider（drain 屏障）+ RenderGate（在飞计数）+ Flush 收敛到安全点（StopPlayback/StartPlayback/渲染与导出段尾部）。VST3 单文件探测进程外化（vst_probe.exe --vst3，崩溃只杀探针）。LoadAtAsync（原生加载移出 UI 线程）。

**B. 格式显式化**：AudioSettings 从零引用死代码变为事实来源（属性化+Configure），ISignalSource 默认接口成员 SampleRate/Channels。信号链/输出/播放/导出层 44100 硬编码清零。**素材层/渲染器 44100 是引擎契约，明确不动**。

**C. 异步渲染+seek 缓存**：PhraseRenderCache（LRU 256MB，key=RenderPhrase.hash 自动失效）+ RenderRequests 流水线（DOP=NumRenderThreads 默认 2，渲染器线程安全未验证前保守）+ 两批播放策略（批 1 播放头前方完成即 StartPlayback，批 2 后台，取消返回 null 防旧链启动）。

**D. 导出统一+命令接线**：三路径统一立体声（偏离上游分轨单声道惯例，已标注）；RenderWindow 混音改离线路径（删 RecordingAdapter/DrainExport）；ExportSession 统一会话；VST 槽位命令化（AddVstSlot/RemoveVstSlot/SetVstPluginCommand/ToggleVstBypassCommand，undo 恢复实例+参数）。

**后续踩坑**（修复提交）：
- vst_probe.exe 部署需全套产物（apphost 缺 dll/runtimeconfig 启动即失败）
- DocManager.ExecuteCmd 拒组外命令（No active UndoGroup）——UI 命令必须包 StartUndoGroup/EndUndoGroup
- Undo/Redo 通过 Publish(cmd, isUndo) 发布原命令——订阅者要处理 isUndo 才会刷新 UI
- toggle 语义命令不能用 LambdaCommand 固定闭包（撤销后新命令场景行为错），需自定义命令类首捕旧值
- VST 测试共享单例需 [CollectionDefinition(DisableParallelization = true)] 串行

**遗留**：VST2 假支持（只扫描）、VST3 编辑器窗口、素材层 44100 契约、采样率可配置 UI（AudioSettings.Configure 已就绪）、流式导出。

相关：[[sukiui-replacement]]、[[vst-phase-3-prep]]
