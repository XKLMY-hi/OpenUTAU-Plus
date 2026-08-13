---
name: ui-redesign-phase
description: UI 统一重构 — 当前状态、设计决策、待开始 Phase 1
metadata: 
  node_type: memory
  type: project
  originSessionId: 1356e18c-ddc4-4688-962b-eb3824a728f3
---

# UI 完全重铸 — 当前状态

**日期**: 2026-07-31
**分支**: plus-develop
**计划**: `C:\Users\XKLMY\.claude\plans\twinkly-growing-cascade.md`（已批准）

## 已确认决策（2026-07-31 用户拍板）

1. **完全替换原版框架** — 移除 FluentTheme，自建 PlusTheme 模板体系（ControlTheme 键），不引入第三方主题库
2. **最终目标四效果**：①全局自绘边框 ②内嵌子窗口（排除歌手设置/VST/表情界面）③全局亚克力/类玻璃 ④全局过渡动画
3. **预览库 v3.0 太缝合需重统一** — 方向 B：保留玻璃但收束为「浮层 + 面板/卡片」两层，轨道头/strip/侧栏改实色；砍鼠标跟随光斑/多余辉光；统一选中(accent-muted)/hover(表面提亮)；参照微软 UWP/Fluent Design System
4. **可扩展架构**：令牌三层(palette→Plus*语义→消费) + ThemeManager.Apply 单一入口 + ThemeVariant 现代化（兜底开关）+ ControlTheme 键 + MixerTrackStrip/ExpSelector 改 TemplatedControl + ThemeYaml 扩展非颜色令牌 + 图标/字体归口
5. **暖灰暗色色板** — `#1e1e28` 基底 / `#282838` 控件面 / `#383848` 边框 / `#c73a3f` accent（基础不变）
6. **HarmonyOS Sans SC** + **Heroicons Solid**（沿用）

## 分阶段（9 阶段，0-6 本次动工，7-9 路线图）

- **0 设计重统一 v4.0**：重做 `D:\xklmy文件夹\XK\XKLMY\项目\vibe coding\UI\index.html`，冻结最终令牌
- **1 令牌地基 + 主题机制现代化**：Plus.Resources.axaml + ThemeManager.Apply + ThemeVariant + ThemeContractTests
- **2-3 控件自绘**：输入控件 / 菜单导航（「接管即删」Styles.axaml）
- **4 删 FluentTheme + 全量回归**（最高风险）
- **5 可扩展性落点**：TemplatedControl + 图标/字体归口
- **6 视觉精修 v4.0 + 动效 + 画布控件**
- **7-9 路线图**：窗口统一(C4) / Dock 内嵌层 / 窗口级动画

## 2026-08-01 重大转向：Avalonia 12 升级（本文件旧计划已被新方向取代）

**⚠️ 本文件"阶段 0-6 完成"的提交 hash（865bd733 等）与实际 git log 不符**——实际只提交到 Phase 3（`398dee9a`），后续阶段未落地。**新方向（用户 2026-08-01 拍板）**：
- 用户回滚了改乱的部分；确认"输入/按钮类控件自绘（PlusTheme 13 种）+ 其余 Fluent"的分工没问题
- **全局替换翻车 → 改为升级 Avalonia 12 + 引入现成 UI 库**（Semi.Avalonia 候选，试用后决定）
- Avalonia 12 升级已完成（见 [[avalonia-12-upgrade]]），自绘边框按 WindowDrawnDecorations 官方规范重写完成

**PlusTheme 当前状态**：Phase 1-3 已提交（`defdaba6`/`1ecd3fd8`/`398dee9a`）。**2026-08-02 用户拍板：PlusTheme 自绘模板全部退役，SukiUI 接管**（见 [[sukiui-replacement]]）。旧计划中的 Phase 4-9（删 FluentTheme / 清理 Styles.axaml 残留 / 可扩展性 / 视觉精修）**不再按原计划执行**，并入 SukiUI 替换阶段 C：
- Styles.axaml 与 PlusTheme 双套清理（旧块删除）
- 主题机制三轨并存（ThemeManager.Apply 新轨道 vs ThemeEditorViewModel 33 处逐键写根旧轨道 vs CustomTheme.axaml 死文件）→ 统一到 SukiTheme
- 阶段 8-9 旧规划（Dock 内嵌层 / 全局动画）仍可参考，优先级低于 SukiUI 替换



**✅ 阶段 5 完成（安全项）**：`ThemeManager.GetIcon(key)` 图标归口（补 icon-heart/vibrato/window-maximize/window-restore，收敛 FavouriteToggleButton/WindowTitleBar/NotesCanvas）；MixerTrackStrip 静音红→PlusSemanticDanger。字体归口**不做** Window 改绑（ui.fontfamily 是按语言本地化机制，改绑回归 CJK）。

**⏸️ 阶段 5 推迟项（TemplatedControl 转换）**：MixerTrackStrip/ExpSelector → TemplatedControl **按计划回退条款推迟**，待用户预览决策。实验结论：**ControlTemplate 内 `x:Name` 不生成字段**，需完整 OnApplyTemplate 部件解析（`e.NameScope.Find<T>("Name")`）+ 构造接线移入 OnApplyTemplate + Track 加载时序调整，无法离屏验证。转换步骤已记录（见下文"阶段 5 推迟项操作指南"）。

## 阶段 5 推迟项操作指南（MixerTrackStrip → TemplatedControl）

1. .axaml：`<UserControl>` → `<TemplatedControl>` + `<TemplatedControl.Theme><ControlTheme x:Key="{x:Type controls:MixerTrackStrip}" TargetType="...">`（Width/MinHeight 移入 Setter），内容包进 ControlTemplate。
2. .cs：`class MixerTrackStrip : TemplatedControl`；11 个命名部件（FaderBox/ThumbBar/LevelFill/ColorBar/TrackNameLabel/MuteBtn/SoloBtn/PanSlider/VolValueLabel/FxEntryBtn）改 `private Border? ...` 等 nullable 字段。
3. `OnApplyTemplate(TemplateAppliedEventArgs e)` 用 `e.NameScope.Find<T>("Name")` 解析全部部件 + FaderBox SizeChanged/事件接线（原构造函数里的）；防重复 apply。
4. `Track` setter 只存 track+vm，`LoadTrackData()` 改由 OnApplyTemplate 在部件就绪后调用；`MixerTrackStrip(UTrack)` 的消息订阅移到 OnApplyTemplate 之后。
5. PartContractTest：实例化 + ApplyTemplate + 断言各部件存在 + fader 交互（DbToTop 数值）。

**设计决策（2026-08-01 终定）**：**彻底放弃半透明/玻璃/亚克力，全部纯色**（"全部都别做半透明和玻璃了"）。层次靠 1px 描边 + 阴影，不靠透明度。PlusGlass* 令牌保留但不用。亚克力默认禁用。

**遗留小 bug（用户说以后改）**：个别控件微调。

## 关键约束

- **每阶段闸门**：完成验收后必须先让用户实机预览确认，再进下一阶段
- **PART 矩阵**是验收清单（详见计划文件）；「接管即删」防双重写入
- **不触碰** WindowEx 的 ExtendClientArea/WindowDecorationMargin 语义（PianoRoll.axaml:15 绑定依赖）
- **陷阱**：`ThemeManager.TryGetString` 用 `ThemeVariant.Default`（不能 `ActualThemeVariant`，后台线程调用会跨线程崩溃）；`Control.BoxShadow` 是 `BoxShadows` 类型；ToggleSwitch 的 `PART_SwitchKnob` Canvas 必须 `HorizontalAlignment="Left"`（否则 Stretch 致 Bounds.Width 变大 knob 飞右）

**Why:** 用户对 Fluent 半成品 UI 和"缝合"的 v3.0 预览不满，要求完全替换 + 更可扩展架构。
**How to apply:** 实施严格按计划文件 9 阶段推进，每阶段结束跑 verify skill + 用户预览闸门。阶段 4 从"补全剩余控件模板"开始。
