---
name: project-overview
description: OpenUTAU Plus 项目核心概况
metadata: 
  node_type: memory
  type: project
  originSessionId: e0a03d4d-5f8d-4646-8e07-4b2464f13baa
---

OpenUTAU Plus 是开源歌声合成平台 OpenUTAU 的增强分支，基于 .NET 8.0 + Avalonia UI 11.x + ReactiveUI。

## 当前分支
- `plus-develop` — 所有 Plus 改动的主开发分支
- `master` — 与上游 openutau/OpenUTAU:master 同步

## 分支健康度（2026-07-31 审计）
- master = upstream/master（gh API 验证 27573ac5 一致）；plus-develop = master 纯线性超集（0/89 无分叉，0 merge commit），提交全 Conventional Commits，CI 覆盖 master+plus-develop
- ⚠️ **master 自 clone(2026-07-14) 后从未做过同步 merge**，目前恰好一致只因上游 3 周未动。上游一推进 plus-develop 会悄悄落后——每次开发会话开始先 `git fetch upstream && git checkout master && git merge upstream/master && git checkout plus-develop && git merge master`
- fork 默认分支 = plus-develop；origin 无 master 分支（合理）

## 近期主要工作
- **2026-08-12 UI 改造全部结束**（用户确认）：SukiUI 迁移 A-E 全收官（含欢迎页 Suki 化、VST 侧栏选项卡），所有待办清空；ThemeEditorWindow 崩溃已解决
- **2026-08-04 0.0.3-beta 发布**（b1d4c891）：Inno Setup 安装器 + 构建流水线、与原版共存（数据目录独立+声库共享）、vst_probe 独立部署修复发布版扫描、HarmonyOS Sans SC 字体打包、单实例按 exe 路径匹配、README/偏好设置署名
- **2026-08-02 SukiUI 阶段 B1 完成**（f3b23f52 → ae5a1e5c 共 3 提交）：WindowEx 基类迁移 SukiWindow（34 窗口零 xaml 改动，装饰经继承自动生效）；x:Name 坑实证排除（编译路径正常）；音符属性面板透明化；顶部留白 8px；测试 252/253。期间踩坑：失败构建产出双份 avares 损坏 dll（AssemblyDescriptor 崩溃）。详见 [[sukiui-replacement]]
- **2026-08-02 CLAUDE.md 更新**（30774af1）：Avalonia 12/SukiUI 技术栈 + SukiUI 现状与约定 + 自检流程规定（自截图一般不用，识图由用户提供截图时使用）
- **2026-08-02 SukiUI 阶段 A 收官**（a6c8ea11 → 537671ed 共 11 提交，全部已提交）：主题接入（暖灰 #c73a3f）+ SukiOverrides 收敛层（HarmonyOS 13px/黑字修复）+ 紧凑菜单体系（SukiCompactMenu）+ 退出确认 Suki 玻璃卡片（BlurEffect+半透明）+ 测试污染修复（246/247 全绿）。详见 [[sukiui-replacement]]
- **2026-08-02 亚克力全清除 + 跟随主题色渐变背景**（已提交 a6c8ea11）：删 WindowEx 模糊逻辑/Preferences 字段/偏好设置 UI/6 字符串/AcrylicTintBrush；新增 PlusSurfaceBgTop/Bottom 令牌 + PlusBrushWindowBackground 3-stop 渐变画刷（深 #1e1e28→#282029 / 浅 #f5f2f0→#f1e9e7），契约测试同步更新
- **2026-08-02 视觉自检能力**：用户新增 image-recognize/verify skill（截图+GLM-4V 识别闭环），但**不能手操界面**（交互验证靠用户实机）。见 [[image-recognize-capability]]
- **2026-08-01 Avalonia 12 升级完成**（11.2.4 → 12.1.0，3 提交已推送）：breaking changes 全修复、自绘边框按 WindowDrawnDecorations 官方规范重写、README 更新。详见 [[avalonia-12-upgrade]]
- 2026 年 7 月：PlusTheme 自绘体系 Phase 1-3（令牌地基 + 输入控件 + 菜单浮层）、全实色决策、VST3 插件支持、混音台、渲染窗口重构、全面汉化 + 暖灰主题 + Heroicons + HarmonyOS Sans SC

## 关键文件
- 解决方案：`OpenUtau.sln`
- 主应用 UI：`OpenUtau/` (Avalonia Views/ViewModels)
- 核心逻辑：`OpenUtau.Core/`
- 本地化：`OpenUtau/Strings/Strings.zh-CN.axaml`

**Why:** 新会话需要快速了解项目背景和当前进展。
**How to apply:** 每次新会话开始时回顾此文件了解项目状态。
