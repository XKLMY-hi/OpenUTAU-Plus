---
name: vst-gui-thread-fix
description: 2026-08-04 重大修复——VST 大插件 GUI 卡死（VST 专用线程方案），含四轮失败方案教训
metadata: 
  node_type: memory
  type: project
  originSessionId: 84d7e7cc-b226-48d1-935d-a3e22b84ee0a
---

# VST GUI 卡死重大修复（2026-08-04，commit 529e0d8d）

## 症状

Persistent Q / TDR Nova 等大插件打开原生 GUI 时应用卡死/未响应；OTT 轻量插件正常。
用户曾误以为"之前修复成功过"——实为只测过 OTT，大插件从未成功打开过。

## 根因链（最终确认）

1. `LoadAtAsync` 用 `Task.Run` 在**任意线程池线程**执行 `vst_load` → controller 创建线程不固定
2. `attached`（打开编辑器时）在 **UI 线程**执行 → **controller 线程 ≠ attached 线程** → 线程敏感插件（Persistent Q）挂起
3. UI 线程同步 attached 秒级阻塞消息泵 → Windows 判定未响应

## 四轮失败方案（教训）

| 方案 | 结果 | 失败原因 |
|------|------|---------|
| UI 线程同步 attached | 卡死 | controller 线程不匹配（线程池）+ UI 阻塞 |
| 窗口线程化（独立线程建窗+attached） | Persistent Q 打不开 / Nova 卡死 / OTT 正常 | attached 线程 ≠ controller 线程，插件线程 ID 检查失败 |
| 三线程（窗口线程+attached 线程+UI 返回） | 全插件无编辑器 | attached 线程无消息循环，插件等消息死锁 |
| MsgWaitForMultipleObjects + PeekMessage 循环 | Persistent Q/Nova 正常，**OTT 渲染坏** | **MsgWait 漏掉跨线程 SendMessage 唤醒** → 插件渲染线程 SendMessage 卡死 → 界面不刷新/控件缺失 |

## 最终方案：VST 专用线程（`OpenUtau.Core/Vst/VstThread.cs`）

- 每插件一个专用 **STA** 线程：**vst_load → createView/attached → 编辑器窗口消息循环全在同一线程**（线程敏感检查通过 + 插件消息自身泵出）
- 消息循环 = **GetMessage 阻塞式**（对跨线程 SendMessage 通知渲染的插件可靠——OTT 验证）
- 宿主命令唤醒 = **隐藏哨兵窗口 + PostMessage(HWND)**——不依赖线程 ID
- `VstEffect`：Load/Setup/编辑器/状态/卸载 marshal 到专用线程；**音频 Process 保持直连**（VST3 processor 与 controller 分离是标准）
- 编辑器打开异步化（`OpenNativeEditorAsync`，UI 不阻塞）
- C++ 侧删除无效 detach 消息循环线程（**窗口消息路由到创建线程的队列，detach 线程收不到**——26ef143e 起就是摆设）；WM_DESTROY 不 PostQuitMessage

## 踩坑记录

- **PostThreadMessageW 的 1444 竞态**：①参数必须是 **OS 线程 ID**（`GetCurrentThreadId`），不是 `ManagedThreadId`；②线程未进入 GetMessage/已退出时投递失败 → 命令静默丢失 → Load 返回零。**弃用，改哨兵窗口**
- **`dotnet test --no-build` 用旧 dll**：改 Core 后不 rebuild 测试项目，诊断日志全打在旧二进制上，白查两轮
- **xUnit 吞 Console.WriteLine**：诊断输出要放异常消息里或写文件
- 测试里 VstEffect 不 Dispose → VstThread 线程泄漏 → 进程退出异常

## 桥接层手动测试工具（VstTest/Program.cs）

分层定位的关键手段（绕开应用层验证桥接本身）：
- `--raw <vst3路径>`：Main 线程直弹 GUI + GetMessage 循环（Persistent Q attached 593ms / OTT 187ms 正常——证明桥接无问题）
- `--vstthread-gui <路径>`：VstThread 路径（应用同路径）复现/验证
- `--threadtest` / `--threadtest2`：VstThread 冒烟

## 验证

- VST 测试 27/27 稳定（3-4 连跑）
- 应用内：Persistent Q / TDR Nova / OTT GUI 全部正常、UI 不卡、导出正常
- 关联 [[audio-pipeline-refactor]]（同期渲染管线重构）、[[sukiui-replacement]]（UI 更新背景）
