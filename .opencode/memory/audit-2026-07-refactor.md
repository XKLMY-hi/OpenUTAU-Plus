---
name: audit-2026-07-refactor
description: 2026-07-30 全面审计结论与重构分阶段计划，报告落盘于 docs/AUDIT.md
metadata: 
  node_type: memory
  type: project
  originSessionId: 1356e18c-ddc4-4688-962b-eb3824a728f3
---

2026-07-30 对 plus-develop 全代码库做了 5 维并行审计，完整报告落盘于 `docs/AUDIT.md`（含 file:line 锚点）。会话由我接管重构。

## 健康度
架构分层 🔴 / VST 宿主 🔴中下 / Mixer+SignalChain ⚠️ / UI ⚠️中下 / 测试 🔴 / 格式+CI ⚠️中下。功能能跑但有若干"静默错"缺陷；最近 25 个 fix 提交暴露"靠试错、无抽象无测试"模式。

## 必须优先修复的缺陷（编号见 AUDIT.md）
- 数据丢失：A1 SaveAllStates 从未被调用（VST GUI 改参保存即丢参）、A2 导出含 VST 头部静音+进度条不动+无取消、A3 autosave 调错函数、A4 版本闸口用错常量、A5 Base64 state 无容错、A6 VST2 UID 用 GetHashCode 不可移植
- 崩溃/并发：B1 音频线程 use-after-free（VstEffect.Dispose↔Process，无 grace period）、B2 全局 g_handler.inst 参数串台、B3 无锁队列实现错误（非 SPSC/内存序错/O(4096)扫描）、B4 probe 进程内加载 DLL 无崩溃隔离、B5 P/Invoke 字符集未声明中文路径必崩
- 架构债：C1 Solo/Mute/VST 插槽不可撤销、C2 VST 三窗口无 ViewModel、C3 MixerTrackStrip 越层、C4 窗口体系二分(12 WindowEx+20 plain)、C5 WindowDecorationMargin 重复 22 处、C6 拖拽双实现死代码、C7 MixFxSource 死代码

## 重构分阶段（用户已确认"先出方案再动工"，尚未选定起步阶段）
- 阶段0 止血：A1-A6、B5、C7、C8、C9、B4（高价值低风险）
- 阶段1 VST 并发安全+跨平台降级：B1/B2/B3、OS 守卫、B6、修空壳 LoadPlugin、Setup/Activate 时机
- 阶段2 分层+测试：C1-C6、D1 plus-develop 加 PR-Test、D5 补 UMixFx/MixerViewModel 单测+Avalonia.Headless
- 阶段3 远期愿景地基：IEffect 加 Latency、Bus/Send/Sidechain、路由数据驱动、采样率集中（README 路线图，单独立项）

## 用户工作约束
- 参照 README 设计规划为硬约束（愿景=人声中心 DAW；近期 VST 音源+跨平台；远期 Aux/侧链/PDC/VCA）
- 保持上游可合并、USTX 向后兼容、OpenUtau 命名空间不变、每提交可构建可运行
- 工作节奏：先出方案再动工

相关：[[vst-phase-3-prep]]（VST C# 侧早已就绪，本审计是对其实现层的深度复核）
