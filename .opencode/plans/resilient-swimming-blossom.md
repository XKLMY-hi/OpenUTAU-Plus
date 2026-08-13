# PlusTheme 自建模板体系 — 摆脱 Fluent 骨架束缚

## Context

用户在预览库 v3.0（模拟玻璃 + 辉光）基础上，对"在 Fluent 模板上爆改"的方式不满（探索确认：只有 3 个控件是完整自绘模板，其余全靠 Fluent 兜底，`/template/` 选择器依赖 Fluent 内部 PART 名，像戴着镣铐跳舞）。决策：**移除 FluentTheme，自建 PlusTheme 完整模板体系**，视觉完全自控。

已探索确认的关键事实：
- **DAW 自绘 Canvas 核心**（PartsCanvas/NotesCanvas/PhonemeCanvas/TrackBackground/TickBackground/ExpressionCanvas/WaveformImage/OtoPlot/PartControl）与 Fluent **完全解耦**——重写 Render，读 ThemeManager 静态画刷，只依赖 Brushes.axaml 资源名。零改动。
- **主题切换**（App.SetTheme → ThemeManager.LoadTheme）与 Fluent 解耦，可沿用。耦合点：ThemeManager 用 `ThemeVariant.Default` 查约 20 个资源名（全在 Brushes.axaml）。
- 必须由 PlusTheme 承接的控件（依赖 Fluent 模板）：Button 153 / TextBox 45 / ComboBox 48 / CheckBox 35 / ToggleSwitch 20 / ToggleButton 17 / Slider 47 / ListBoxItem 24 / MenuItem 146 / ContextMenu 15 / RadioButton 9 / ProgressBar 4 / Expander 4 / ScrollBar 4 / ToolTip（附加属性大量）。
- DataGrid(7)、ColorPicker(31) 保留官方 Fluent 主题包，PlusTheme 只做颜色覆盖。
- TabItem 无视图使用（仅样式遗留），低优先级。

## 核心决策

1. **路线 A**：Phase 1-3 Fluent 兜底（排 PlusTheme 前），Phase 4 删除——每阶段可编译运行，无空白窗口。
2. **文件组织**：新建 `OpenUtau/Themes/` 目录，按控件分文件 + 根 `PlusTheme.axaml` 合并。
3. **Brushes.axaml 的 SystemControl\* 键继续复用**（ThemeManager + 15 视图按名引用），只替换模板层。
4. **PART 名兼容**：现有 `/template/` 选择器硬依赖的部件名在 PlusTheme 模板中必须同名保留。

---

## 阶段 1：骨架 + Button/TextBox/ComboBox

**新增**：`Themes/Plus.Resources.axaml`（令牌层：圆角/阴影/玻璃兜底画刷）、`PlusTheme.axaml`（根合并）、`Plus.Buttons.axaml`、`Plus.TextBox.axaml`、`Plus.ComboBox.axaml`

**修改**：
- `App.axaml`：MergedDictionaries 加 `Plus.Resources.axaml`；Styles 加 `<StyleInclude Source="/Themes/PlusTheme.axaml"/>`（排 Fluent 后）
- `Colors/Brushes.axaml`：补齐缺失键（`SystemControlForegroundBaseLowBrush`、`SystemControlBackgroundAltMediumBrush`、`TextControlForegroundDisabled`、`MenuFlyoutItemForegroundPressed`、`SliderHorizontalThumbWidth/Height` 等）
- `Styles/Styles.axaml`：删除全局 TextBox/Button/ComboBox 与 PlusTheme 重复块；类变体（primary/outline/danger/clear/titleBarBtn）迁移到 Plus.Buttons 内

**模板规格**：
- Button：外 Border（玻璃阴影/内高光/描边）+ `ContentPresenter#PART_ContentPresenter`；变体 primary/outline/danger/ghost
- TextBox：`Border#PART_BorderElement`（**必须保留**，PianoRollStyles:175 依赖）+ ScrollViewer + Watermark
- ComboBox：`Border#Background` + `PART_ContentPresenter` + `Path#DropDownGlyph`（均硬依赖）+ Popup 弹层玻璃配方 D

**验证**：build + 启动 + PreferencesDialog/ExpSelector 下拉 + 主题切换三态 + `Button.primary/danger/titleBarBtn` 三态。

## 阶段 2：Selection/Slider/ListBox

**新增**：`Plus.Selection.axaml`（ToggleSwitch/CheckBox/RadioButton）、`Plus.Slider.axaml`（默认模板+ProgressBar）、`Plus.ListBox.axaml`

**修改**：Styles.axaml 删除全局 CheckBox/ListBoxItem/RadioButton 块；保留 `CheckBox.menu`、`Slider.fader`；ToggleSwitch 覆盖迁入 Plus.Selection。

**PART 硬依赖**：`SwitchKnobBounds`/`OuterBorder`（ToggleSwitch，PreferencesDialog:45,48）、`NormalRectangle`/`CheckGlyph`（CheckBox）、`PART_DecreaseButton`（Slider，Styles:577）、`ContentPresenter`（ListBoxItem）。

**验证**：PianoRoll 菜单 CheckBox、PreferencesDialog ToggleSwitch、RenderWindow RadioButton、MixFxDialog 默认 Slider、混音台 ListBoxItem 选中态。

## 阶段 3：Menu/Expander/ToolTip/ScrollBar/ProgressBar/TabItem

**新增**：`Plus.Menu.axaml`、`Plus.Expander.axaml`、`Plus.ToolTip.axaml`、`Plus.ScrollBar.axaml`、`Plus.ProgressBar.axaml`、`Plus.TabItem.axaml`

**PART 硬依赖**：`PART_InputGestureText`/`PART_ChevronPath`（MenuItem）、`PART_PopupBorder`（MenuFlyoutPresenter）、`ExpanderHeader`/`ExpandCollapseChevronBorder`（Expander）、`Thumb.thumb`/`RepeatButton.line`/`Path#Arrow`/`Rectangle#TrackRect`（ScrollBar.music）。

**验证**：MainWindow 菜单、右键 ContextMenu（配方 D）、NotePropertiesControl Expander、全局 ToolTip、PianoRoll ScrollBar.music、SplashWindow ProgressBar。

## 阶段 4：删 Fluent + 清理 + 全量回归

**修改**：
- `App.axaml`：删 `<FluentTheme/>`
- `OpenUtau.csproj`：删 `Avalonia.Themes.Fluent` 包引用
- `Styles/Styles.axaml`：删残余 Fluent `/template/` 状态选择器 + `Popup > Border` 全局圆角
- `Brushes.axaml`：对照 DataGrid/ColorPicker Fluent.xaml 补齐缺失键

**验证**：全量控件清点（Button153/TextBox45/ComboBox48/CheckBox35/ToggleSwitch20/ToggleButton17/Slider47/MenuItem146/ContextMenu15/RadioButton9/DataGrid7/ColorPicker31）+ Fluent 删除后空白扫描 + `dotnet test`（AppTest 加载 App.axaml 是 XAML 解析回归门）。

---

## 视觉语言（预览库 v3.0 规格）

**令牌**（加进 Light/Dark 字典 + Plus.Resources 兜底）：`PlusCornerRadiusXS/SM/MD/LG/XL`（4/6/8/12/16）、`PlusBgSurface/Hover/Pressed/Disabled`、`PlusBorderDefault/Hover/Focus/Glass/GlassStrong`、`PlusGlassPanel/Hover/Active/Popover/Strong/Header/InsetBottom/Input`、`PlusElevContact/Float/Raised/Popup`（BoxShadows，含 inset 内高光）、`PlusFocusRing`（`0 0 0 3 #2EDADDE6`）、`PlusGlowRing`（`0 0 0 1 #80DADDE6, 0 0 10 0 #47DADDE6`，Win10 辉光）、`PlusGlowDanger`、`PlusMotionQuick/Normal`（120/180ms）。

**玻璃四配方**（映射 Avalonia BoxShadow）：
- A 弹窗：`#EB1E2028` + `#1AFFFFFF 1px` + 圆角 12 + 三叠阴影
- B 面板：`#9E1E2028` + 圆角 16 + 内高光 + raised
- C 悬浮工具栏：顶部渐变 + 圆角 16 + 强调内高光
- D 菜单/Tooltip：`#F0262632` + 圆角 12/6 + 弹出投影

## 关键文件

- `OpenUtau/App.axaml`（装配点）
- `OpenUtau/Themes/*`（新增 13 个文件）
- `OpenUtau/Styles/Styles.axaml`（迁移源，三分类处置）
- `OpenUtau/Colors/Brushes.axaml`（键补齐 + 玻璃令牌兜底）
- `OpenUtau/ThemeManager.cs`（LoadTheme 资源名契约，不可破坏）
- `OpenUtau/Controls/ExpSelector.axaml`（自绘 ComboBox 模板，ComboBox 兼容试金石）

## 风险

1. **视图级硬 PART 依赖失效**（最高）：以 PART 矩阵为验收清单逐项 grep 确认
2. **ThemeManager 资源名耦合**（高）：Brushes.axaml 保留即不破；每阶段主题切换后断言画刷正确
3. **Fluent 删除后空白风险**：走路线 A，Phase 4 前全控件清点
4. **样式双重写入**：每阶段从 Styles.axaml **删除**被接管块而非叠加
5. **DataGrid/ColorPicker Fluent 残留键**：Phase 4 对照其 Fluent.xaml 逐键补齐
6. **测试回归**：每阶段跑 `dotnet test`（AppTest 是 XAML 解析门）
7. **TreatWarningsAsErrors**：每控件文件单独 build 收敛警告

## 验证（总）

每阶段：`dotnet build` 全绿 → `dotnet run` 无 [ERR] → `dotnet test` → 截图对照预览库 v3.0。Phase 4 全量控件清点 + 主题 Light→Dark→Custom 往返 + verify skill 全流程。
