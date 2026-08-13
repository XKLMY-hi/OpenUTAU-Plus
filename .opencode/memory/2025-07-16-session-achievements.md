---
name: session-2025-07-16
description: 大进展——渲染窗口、实时录制、混音台同步、VST 激活修复、截图、许可证
metadata: 
  node_type: memory
  type: project
  originSessionId: b5fb2a48-571b-4140-8518-8f92e74a5d68
---

# 2025-07-16 会话完成内容

## 渲染窗口 RenderWindow
- 新建 `Views/RenderWindow.axaml` + `.cs`（纯 code-behind，零 MVVM 绑定避免崩溃）
- 输出模式：立体声混缩 / 分轨导出
- 渲染范围：整曲 / 循环区间 / 自定义
- 输出格式：WAV 16/32-bit、采样率 44100/48000
- 轨道勾选 + 效果链开关（VST / 内置 FX / 音量声像）
- 输出路径浏览 + 打开文件夹按钮
- Lucide 图标：分区标题 + 底部操作按钮
- 菜单入口：导出音频 → 渲染...（Ctrl+Shift+R）

## 实时录制导出（解决 VST 导出问题）
- 新增 `OpenUtau.Core/SignalChain/RecordingAdapter.cs`（T 型分流器）
- 走 `RenderProject()` 完整播放信号链，RecordingAdapter 同时写文件 + 回传静音
- 混缩模式默认使用实时录制（含完整 VST）

## 混音台双向同步
- `MixerTrackStrip` → MessageBus 广播 Volume/Pan 变更
- `TrackHeaderViewModel` → 监听 MessageBus 同步
- `_syncing` 防循环标志
- 声像范围统一为 -100~100

## VST 自动激活 + 最近文件
- `UProject.AfterLoad()` 末尾遍历所有 VstSlots 调用 `LoadEffect()`
- `Preferences.AddRecentFileIfEnabled()` 新增 `.ustxp` 分支

## 许可证
- `LICENSE.txt` 追加 "Copyright (c) 2026 OpenUTAU Plus contributors"
- 保留原 "Copyright (c) 2014 StAkira"

## README
- 中英文均添加实机截图（5 张：欢迎页/编辑器/混音台/VST/渲染窗口）
- 移除「导出不含 VST 效果」局限性

## 版本号
- `0.1.568.121`

## 提交记录
- 2ea35ce8 docs: 添加实机截图
- c02508be logo.png 放仓库根目录
- be9db110 LICENSE 更新
- 88a9f844 VST 导出限制移除
- 7c498724 精简渲染窗口
- e304403e 混缩默认实时录制
- b8e717f8 实时录制 + RecordingAdapter
- 5ce3da33 MasterAdapter public
- 69644c50 混音台双向同步
- 30b95c54 VST 自动激活 + .ustxp 最近文件
