---
name: avalonia-12-upgrade
description: 2026-08-01 Avalonia 12 升级完成 — 关键决策、踩坑记录、后续方向
metadata: 
  node_type: memory
  type: project
  originSessionId: 84d7e7cc-b226-48d1-935d-a3e22b84ee0a
---

# Avalonia 12 升级（2026-08-01 完成，已推送到 origin/plus-develop）

## 提交（plus-develop，全部已推送）
- `f301ddac` upgrade: Avalonia 11.2.4 → 12.1.0 迁移
- `71046314` feat(ui): 自绘边框 WindowDrawnDecorations 官方规范重写
- `baecfc1f` docs: README 更新 + 删旧截图

## 关键决策（用户拍板）
- **UI 库**：升级后试用再定（官方 Fluent 过渡，Semi.Avalonia 候选）
- **分支**：feature/avalonia-12 已合并（fast-forward），分支保留
- **ReactiveUI 兼容线**：ReactiveUI.Avalonia **14.7.1**（依赖 ReactiveUI 19.x，保留 System.Reactive/Rx 类型，40+ ViewModel 零改动）。⚠️ 12.x 线依赖 ReactiveUI 24（R3），会摧毁全部 Rx 代码——升级 ReactiveUI 需另立项目
- **xunit v3**：Avalonia.Headless.XUnit 12 依赖 xunit.v3 → `xunit.v3.core` + `xunit.v3.assert` 3.2.2（v3 无 xunit meta 包）

## 自绘边框踩坑记录（WindowDrawnDecorations）
1. **WindowDecorations 值决定部件启停**：None=全禁用（FrameThickness/TitleBarHeight 有效值归零 → 无边框、按钮"挤进"内容区）；必须用 **Full** + ExtendClientAreaToDecorationsHint=true 才启用全部 drawn 部件
2. **模板槽位共用同一 NameScope**：Overlay 和 FullscreenPopover 不能有相同 x:Name（PART_* 只放 Overlay，FullscreenPopover 去名，行为靠 ElementRole）
3. **ElementRole 需设在可命中叶子元素**：DockPanel/StackPanel 上的 role 不传递；空白拖拽区 Border 必须显式 `Background="Transparent"` 才参与命中测试
4. **内容避开装饰区**：ExtendClientArea=true 时内容全窗口，WindowEx.OnApplyTemplate 给 Presenter 统一绑 `WindowDecorationMargin`（最大化自动归零）；无装饰窗口（WindowDecorations=None）跳过
5. **WindowDrawnDecorationsTemplate/Content 是 XAML 内建类型**（默认 xmlns），只有 WindowDrawnDecorations/WindowDecorationProperties 在 `Avalonia.Controls.Chrome`
6. **SplashWindow**：改继承普通 Window + WindowDecorations=None（不继承 WindowEx，完全绕开自绘逻辑）
7. 官方 docs.avaloniaui.net / github.com 在本环境网络不可达，用 WebSearch + 本地 nuget XML 文档查 API

## 其他
- 中文路径下 git push 需 `git -c http.sslVerify=false push`（SSL 证书本地校验失败）
- 全量测试基线：245/246（仅 JaPresampTest 上游预留失败；PluginRunnerTest、StringsTest 偶发，重跑会过）
- CompiledBindings 过渡关闭中（`AvaloniaUseCompiledBindingsByDefault=false`），迁移稳定后可逐步启用

## ⚠️ Rx 6.x 坑（2026-08-02 实证，排查 4 轮）
- **`ObservableExtensions` 在 Rx 6.x 移到了 `System` 命名空间**（`System.ObservableExtensions`，不再是 `System.Reactive.ObservableExtensions`）——`using System;` 即提供 `IObservable.Subscribe(Action)` 扩展
- `System.Reactive.ObservableExtensions` 在 6.0.1 **不存在**（反射确认 MISSING）；`System.Reactive` 包是聚合包（含全部程序集）
- **`Avalonia.Reactive.Observable` 是 internal**（其 Subscribe/Select 扩展对外不可见）——升级时删 `using System;` 会导致 Subscribe 全线失效且报 CS1660（不是 CS1061！症状是"lambda 无法转 IObserver"）
- 症状识别：CS1660 "无法将 lambda 表达式转换为 IObserver<T>" = 扩展方法不可见（只有实例方法 Subscribe(IObserver) 被找到），不是类型错误

## 下一步（已被 SukiUI 替换计划取代，见 [[sukiui-replacement]]）
- UI 库已定：**SukiUI 7.0.2-nightly**（试用完成，渐进替换启动）
- Styles.axaml 残留清理 + 主题三轨并存统一 → 并入 SukiUI 替换阶段 C

相关：[[ui-redesign-phase]] [[audit-2026-07-refactor]]
