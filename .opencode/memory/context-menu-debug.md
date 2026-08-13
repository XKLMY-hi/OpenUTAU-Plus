---
name: context-menu-debug
description: 右键菜单幽灵弹窗/点外不关闭问题 — 2026-08-01 调查进展与根因线索
metadata: 
  node_type: memory
  type: project
  originSessionId: baecbae6-6ccf-42ba-a9ad-9cff5018eb19
---

# 右键菜单问题调查（2026-08-01，未解决，待续）

**现象（用户实机确认）**：①音符右键菜单正常 ②空白处右键仍弹幽灵 ③点外点击不关闭菜单

## 已完成调查（反编译 Avalonia 11.2.4 源码确认）

- `ContextMenu.Open()` 内部 popup：`IsLightDismissEnabled = true` + `OverlayDismissEventPassThrough = true`（反编译结论，**默认应点外关闭**）
- Avalonia 类处理器 `ControlContextRequested`（ContextMenu.cs:393）：检查 `!e.Handled` 后才打开菜单 → 理论上 `ContextRequested` 中 `e.Handled=true` 能抑制自动打开
- `ContextRequested` 在 `Control.OnPointerReleased` 触发（右键**释放**时，条件 `e.Source == this`）
- Popup 的 dismiss 机制：`LightDismissOverlayLayer`（OverlayLayer 内透明层）→ 点击外部 → `CloseCore()`

## 🔴 根因线索（headless 复现）

写了临时测试 `OpenUtau.Test/App/ContextMenuGhostTest.cs`（3 用例，**用后删除**），`dotnet test --filter ContextMenuGhostTest` 复现：

- `OpenAndDismissOnOutsideClick` FAIL：菜单打开即抛 **`Unable to create IPopupImpl and no overlay layer is found for the target control`**（OverlayPopupHost.CreatePopupHost 找不到 overlay layer）
- Popup 需要 **窗口模板里的 `OverlayLayer` 元素**。全项目 grep 结果：`App.axaml` 已无 FluentTheme（阶段 4 删除），**无任何 `TargetType="Window"` 模板、无 `OverlayLayer` 控件**（MainWindow.axaml:591 的 OverlayLayer 是同名自定义 Grid，无关）
- ⚠️ 矛盾点：实机菜单**能打开** → 实机窗口必有模板来源，未查明（headless 测试环境 = Avalonia.Headless.XUnit 最小应用，无主题 → 无窗口模板 → popup 打不开）。**下一步：查实机窗口模板来源**（Styles.axaml / 运行时注入 / WindowEx 代码里 SetTemplate？），以及 LightDismissOverlayLayer 在实机是否被 WindowEx 结构（ExtendClientArea 自绘）影响

## 工作区状态（未提交）

- `OpenUtau/Controls/PianoRoll.axaml`（ContextRequested 事件）
- `OpenUtau/Controls/PianoRoll.axaml.cs`（Opening 精简 + TryEnableLightDismiss + ContextRequested 抑制，已删全部 MenuDiag 诊断）
- `OpenUtau/Themes/Plus.Menu.axaml`（ContextMenu 模板改 ScrollContentPresenter 结构）
- `OpenUtau.Test/App/ContextMenuGhostTest.cs`（临时复现测试，未提交）
- 已删除：MenuLogicCheck.cs / MenuStructureTest.cs（Cecil 反编译+结构测试）、Mono.Cecil 包引用（csproj 已回 HEAD）

## 其他

- 测试基线：252 通过 / 2 失败（JaPresampTest.ToneShiftAltTest、PluginRunnerTest.ExecuteTest，stash 验证为**预先存在**，与本次无关）
- 上次会话残留：`OpenUtau/bin/Debug/net8.0-windows/Logs/log20260801_003.txt`（本次启动日志，无 ERR）
- 调查工具链：`ilspycmd`（已装，~/.dotnet/tools）+ 反编译输出在 `%TEMP%\contextmenu.cs` / `popup.cs` / `control.cs` / `headless-full.cs`（临时文件，可能被清）
- Headless 输入模拟 API：`Avalonia.Headless` 的 `MouseDown/MouseUp(this TopLevel, Point, MouseButton)` 扩展

**How to apply:** 续作时先复现测试（`dotnet test --filter ContextMenuGhostTest`），查实机窗口模板来源，解决 OverlayLayer 问题后再验证 PianoRoll 右键三行为。
