# OpenUTAU Plus UI 完全重铸：自建 PlusTheme + 可扩展架构

## Context

用户目标：UI 完全替换原版框架（移除 Avalonia `FluentTheme`），采用更可扩展的架构。最终要呈现四个效果：
1. **覆盖全局的自绘边框**（全部窗口统一 WindowEx + 自绘标题栏）
2. **内嵌子窗口**（混音台/钢琴窗等 DAW 工作区可 dock 嵌入主窗口；**排除**歌手设置/VST/表情界面）
3. **全局亚克力/类玻璃**（**已定：背景亚克力禁用**，纯色为主；玻璃只用于浮层菜单/弹窗/toast——recipe D）
4. **全局过渡动画**（控件 + 窗口级开合 + 主题切换）

关键前提：现有 UI 预览库 `D:\xklmy文件夹\XK\XKLMY\项目\vibe coding\UI\index.html`（v3.0）**太缝合**——五套视觉隐喻竞争（玻璃/辉光/鼠标跟随光斑/阴影/渐变）、同一语义多副面孔、玻璃用得过散。需先**重统一设计**再建地基。

**结论：不需要重构整个前端框架**。现有 Avalonia + WindowEx 架构能支撑全部四效果。需要的是：① 设计重统一 → ② PlusTheme 视觉地基（令牌+模板+主题机制）→ ③ 四效果基建（窗口统一/Dock 层/窗口动画）。PlusTheme 是地基，是本次动工核心。

**设计方向（已确认，含 2026-08-01 终定）**：**彻底放弃半透明/玻璃/亚克力，全部纯色**（用户决策："全部都别做半透明和玻璃了，太麻烦了"）。窗口/卡片/内容面/弹层（菜单/下拉/ToolTip）全实色（八档明度差分层），层次靠 1px 描边 + 弹出投影，不靠透明度。背景亚克力默认禁用。去掉鼠标跟随光斑与多余辉光；统一选中/hover 语义。参照微软 UWP / Fluent Design System 原则。原方案 B 的"玻璃保留"已废弃，PlusGlass* 令牌保留但不再使用。

---

## 目标架构

### 令牌三层（单向引用，杜绝反向）

```
Layer 1 palette（可主题化原始值）→ Colors/LightTheme|DarkTheme|CustomTheme.axaml
    Color / CornerRadius / BoxShadow / FontFamily / Double / Thickness 字面量
    PlusSurface* 8档 · PlusBorder* 6档 · PlusAccent* 6档 · PlusText* 4档 · PlusSemantic* 5档
    PlusGlass* 8档 · PlusElevation* 4档 · PlusHighlight* · PlusFocusRing · PlusGlow*
    PlusRadius* · PlusControlHeight* · PlusSpace* · PlusFontSize* · PlusMotion*
    + 既有 33 个 legacy 颜色键（保持 ThemeManager/15 视图兼容）
Layer 2 semantic brush（全局画刷）→ Colors/Brushes.axaml + Themes/Plus.Resources.axaml
    <SolidColorBrush Color="{DynamicResource PlusSurfaceBase}"/> 等 PlusBrush*
    + 兼容层：全部 SystemControl*/MenuFlyout*/TextControl*/ComboBox*/Slider*/RadioButton*
      （含 20 个当前由 Fluent 提供的缺失键）
Layer 3 控件模板/视图 → Themes/Plus.*.axaml + Styles.axaml + Views/*
    全部 {DynamicResource Plus*}，禁止字面量颜色/圆角/阴影
```

**关键语义**：Layer 1 是唯一被主题切换改写的一层；Layer 2 只映射；Layer 3 只消费。`Plus.Resources.axaml` 提供全部 Plus* 键**默认值兜底**，任何主题缺键不空白。

### 主题机制（单一真相）

现状：状态散落 4 处（App.axaml 合并字典 / Colors/*Theme.axaml / CustomTheme 逐键写根 / ThemeManager 静态缓存），`ApplyTheme` 手动逐键拷贝。

**方案：`ThemeManager.Apply(themeName)` 单一写入口 + ThemeVariant 现代化**
- palette 字典挂 `ResourceDictionary.ThemeDictionaries { Dark, Light }`，`RequestedThemeVariant` 驱动解析 → **删 ApplyTheme 手动拷贝**；Custom YAML 注册为自定义 ThemeVariant（圆角/阴影/字体/间距一并可主题化）。
- `ThemeManager` 静态画刷（约 40 个字段，Canvas 控件依赖，**绝不能改名**）改为 **schema 驱动投影**：`(key, setter, penFactory)[]` 映射表 + `RebuildProjection()` 从 active variant 重读；缺键打 `[WARN]` 不覆写旧值。
- **兜底开关**：若 ThemeVariant 现代化实测回归风险不可控 → 回退「保留 ApplyTheme 逐键拷贝 + 仅上 ControlTheme 键 + 统一 ThemeManager 入口」（可扩展性 80% 在 ControlTheme 键与令牌层，变体机制是锦上添花）。**计划默认走现代化，Phase 1 末尾用测试裁决。**

### ControlTheme 控件体系（可换肤的框架级机制）

每个自建控件定义 `<ControlTheme x:Key="PlusTheme.Button" TargetType="Button">`，全局默认用 `Theme` 引用；第三方/未来模块换肤 = 一条 `Theme=` 属性覆盖。复合控件（MixerTrackStrip/ExpSelector）改 `TemplatedControl` + ControlTheme。

---

## PART 依赖矩阵（验收清单）

**必须保留同名 PART 的 Fluent 内置模板**（删 Fluent 即失效，需重建）：ToggleButton `PART_ContentPresenter`；ToggleSwitch `SwitchKnobBounds`/`OuterBorder`；ComboBox `DropDownGlyph`/`Background`；CheckBox `Grid`/`NormalRectangle`/`Viewbox`/`CheckGlyph`；ListBoxItem/ComboBoxItem `ContentPresenter`；ScrollBar `TrackRect`/`Thumb.thumb`/`RepeatButton.line`(嵌套 `Border#Root`/`Viewbox`/`Path#Arrow`)；Expander `ExpanderHeader` 嵌套链；TabItem `PART_SelectedPipe`/`PART_LayoutRoot`；TextBox `PART_BorderElement`；MenuItem `PART_ChevronPath`/`PART_InputGestureText`；MenuFlyoutPresenter `PART_PopupBorder`；DataGridRow `DataGridFrozenGrid`。

**必须保留 PART 的自定义模板**：Button `PART_ContentPresenter`；Slider.fader `PART_Track`/`PART_DecreaseButton`/`PART_IncreaseButton`/`thumb`；ListBoxItem.toolbar `PART_LayoutRoot`/`PART_ToolIcon`/`PART_SubToolArrow`；RadioButton `OuterEllipse`/`CheckOuterEllipse`/`CheckGlyph`；ExpSelector `Background`/`DropDownGlyph`/`PopupBorder`/`PART_Popup`/`PART_ItemsPresenter`；LyricBox `PART_Box`/`PART_Suggestions`。

**僵尸选择器**：ExpSelector.axaml:108 `Border#DropDownOverlay`（指向不存在的 Fluent 部件）→ 删。

## Fluent 缺失键补齐清单（删 Fluent 即断链）

未本地定义：`SystemControlForegroundBaseLowBrush`（21 处）、`SystemControlBackgroundAltMediumBrush`（2 处）、`MenuFlyoutItemForegroundPressed`、`ControlContentThemeFontSize`、`ComboBoxThemeMinWidth`、`ComboBoxMinHeight`、`ComboBoxPadding`、`ComboBoxDropDownBackground/BorderBrush`、`ComboBoxDropdownBorderThickness/Padding`、`SliderHorizontalThumbWidth/Height`、`RadioButtonBorderThemeThickness`、`RadioButtonOuterEllipseFill/Stroke(+PointerOver)`、`TextControlForegroundDisabled`、`AutoCompleteListPadding`。全部补进 Brushes.axaml / Plus.Resources.axaml。

**DataGrid / ColorPicker**：保留官方 Fluent NuGet 主题包（已定决策），PlusTheme 只做颜色覆盖（DataGrid 6 处、ColorPicker 22 处）；Phase 4 对照其 Fluent.xaml 逐键补齐。

---

## 分阶段实施

### 阶段 0 — 设计重统一 v4.0【本次动工 · 设计交付】
重做 `D:\xklmy文件夹\XK\XKLMY\项目\vibe coding\UI\index.html`：
- 玻璃收束为两层规范：**浮层**（菜单/popover/弹窗/toast，配方 D 系）+ **面板/卡片**（配方 A/B）。轨道头/strip/侧栏/卡片改**实色表面档**（八档明度差，不靠透明度分层）。
- 砍鼠标跟随光斑（`.glow-follow`/`.glow-spot`）；辉光只留主按钮 focus；hover 统一「表面提亮一档 + 不浮起」（浮层容器才动阴影）。
- 统一选中语言：唯一 = accent-muted 底 + accent 文字（+可选左 accent 竖条）；删菜单 accent 实心 hover / Tab 底部条变体。
- 轨道色（18 色板）与语义色（success/warning/danger/info）分离。
- 图标统一 Heroicons（预览库去 emoji）；字号/间距走令牌。
- 产出：**v4.0 最终令牌集**（Phase 1 的 Plus* 值以本阶段为准）+ 控件/页面规格。
- 验收：浏览器打开预览库对照四条纪律逐项走查；用户确认设计定稿后冻结令牌值。

### 阶段 1 — 令牌地基 + 主题机制现代化【本次动工 · 核心】
- **亚克力默认禁用**：`WindowEx.ApplyBlurSettings` 默认 `TransparencyLevelHint=[None]`，窗口背景实色 `PlusSurfaceBase`；仅浮层（菜单/弹窗）保留玻璃配方 D。
- 新增 `OpenUtau/Themes/Plus.Resources.axaml`（Plus* 全部默认值兜底）；`App.axaml` MergedDictionaries 加入。
- `Colors/LightTheme|DarkTheme|CustomTheme.axaml` 补 Plus* 令牌（值=阶段 0 v4.0 冻结值）；`Brushes.axaml` 补 20 个兼容键 + PlusBrush* 层。
- `ThemeManager.cs`：`LoadTheme`→`RebuildProjection`（schema 驱动）；新增 `Apply(themeName)`；`App.axaml.cs:SetTheme` 改调它。
- ThemeVariant 现代化（palette 挂 ThemeDictionaries，删 ApplyTheme 拷贝）；**若回归风险失控回退保守方案**。
- `CustomTheme.axaml.cs`：删逐键写根，改 `BuildPalette`；`ThemeYaml` 扩展非颜色令牌（全 nullable）+ legacy→Plus 自动映射。
- 新增 `OpenUtau.Test/App/ThemeContractTests.cs`（键解析 + 三态往返 + CustomTheme 向后兼容）。
- 验收：`dotnet run` 无 [ERR]；三态主题切换行为不变；`dotnet test` 全绿。

### 阶段 2 — 交互输入控件自绘【本次动工】
- 新增 `Plus.Buttons.axaml`(Button+ToggleButton)、`Plus.TextBox.axaml`、`Plus.ComboBox.axaml`、`Plus.Selection.axaml`(ToggleSwitch/CheckBox/RadioButton)、`Plus.Slider.axaml`(Slider+ProgressBar)；`PlusTheme.axaml` 根合并。
- `Styles.axaml` **接管即删**被接管全局块；Button 类变体(primary/outline/danger/clear/titleBarBtn)迁入。
- `PianoRollStyles.axaml`：RadioButton 模板迁全局，保留 SelectedTrack 覆盖。
- 验收：PreferencesDialog/ExpSelector/混音台/MixFxDialog 走查 + `dotnet test`。

### 阶段 3 — 菜单/导航/反馈自绘【本次动工】
- 新增 `Plus.Menu.axaml`、`Plus.ScrollBar.axaml`、`Plus.Expander.axaml`、`Plus.TabItem.axaml`、`Plus.Containers.axaml`(玻璃配方)；`ScrollBar.music` 迁入；DataGrid 颜色覆盖改 Plus 令牌。
- `Styles.axaml` 删 Menu/MenuItem/ContextMenu/ScrollBar/Expander/TabItem/ToolTip 旧块。
- 验收：MainWindow 菜单、右键 ContextMenu、PianoRoll ScrollBar.music、SplashWindow ProgressBar + `dotnet test`。

### 阶段 4 — 删 FluentTheme + 键审计 + 全量回归【本次动工 · 最高风险】
- `App.axaml` 删 `<FluentTheme/>`；Styles 重排（PlusTheme → DataGrid/ColorPicker Fluent → Styles 应用级覆盖）。
- `OpenUtau.csproj` 删 `Avalonia.Themes.Fluent` 包引用。
- `Styles.axaml` 删残余 Fluent `/template/` 穿透选择器 + `Popup > Border` 全局圆角；清 ExpSelector 僵尸选择器。
- 对照 DataGrid/ColorPicker Fluent.xaml 逐键补齐（测试 1 驱动）。
- 验收：全控件清点（Button153/TextBox45/ComboBox48/CheckBox35/ToggleSwitch20/ToggleButton17/Slider47/MenuItem146/ContextMenu15/RadioButton9/DataGrid6/ColorPicker22）；`dotnet test` 全绿（删除后红绿灯）。

### 阶段 5 — 可扩展性落点【本次动工】
- `MixerTrackStrip`、`ExpSelector` 改 `TemplatedControl` + ControlTheme（注册 `PlusTheme.*` 键）；code-behind 换 `GetTemplateChild`。
- 图标：`ThemeManager.GetIcon(string)`；收敛 `Geometry.Parse` 到 Icons.axaml（FavouriteToggleButton.cs:20、NotesCanvas.cs:431、ViewScaler.axaml.cs:58、WindowTitleBar.axaml.cs:44/46）。
- 字体：`PlusFontFamily`/`PlusFontFamilyMono` 令牌；`Styles.axaml:31` Window 改绑；各 Strings 增补本地化栈。
- 复合控件硬编码颜色/圆角扫一遍改 Plus 令牌。
- 验收：混音台/ExpSelector 外观不变但可被主题替换；`dotnet test`（PartContractTest 覆盖 MixerTrackStrip PART）。

### 阶段 6 — 视觉精修 v4.0 + 动效 + 画布控件【本次动工】
- 代码画布新增 `PlusBrushCanvasDeep`（bg-deep 实色）接入 NotesCanvas/TrackBackground/TickBackground/PhonemeCanvas/ExpressionCanvas 等（替换直接读 `BackgroundBrush`）。
- 玻璃四配方在 Plus.Containers.axaml 落地；Preferences/Welcome/Overlay/Transport 走查对照 v4.0。
- 动效令牌接入 ToggleSwitch/CheckBox/Button/ComboBox pop。
- 验收：截图对照 v4.0 逐 token；Light→Dark→Custom 往返无缺失键。

### 阶段 7-9 — 四效果基建【路线图 · 本次不动工】
- **阶段 7（①全局自绘边框）**：剩余 ~20 个对话框迁 WindowEx + WindowTitleBar（审计 C4）；`WindowDecorationMargin` 收口。
- **阶段 8（②内嵌子窗口）**：Dock 面板容器层（混音台/钢琴窗等；**排除**歌手设置/VST/表情界面）；复用混音台已有 Ctrl+M 内嵌/分离双模式经验。
- **阶段 9（④全局过渡动画）**：窗口开合动画 + 主题切换 crossfade + 面板过渡。

---

## 关键文件

- `OpenUtau/App.axaml` + `App.axaml.cs`（装配点、SetTheme、删 FluentTheme）
- `OpenUtau/ThemeManager.cs`（投影化重构 + `Apply` 单一入口）
- `OpenUtau/Styles/Styles.axaml`（676 行迁移源，「接管即删」操作对象）
- `OpenUtau/Styles/PianoRollStyles.axaml`（RadioButton 迁出、ListBoxItem.toolbar 保留）
- `OpenUtau/Colors/LightTheme|DarkTheme|CustomTheme.axaml` + `Brushes.axaml`（令牌 + 兼容键）
- `OpenUtau/Colors/CustomTheme.axaml.cs`（ThemeYaml 扩展 + BuildPalette）
- `OpenUtau/Themes/*`（新增 13 个文件，阶段 2-3 逐步建立）
- `OpenUtau/Controls/MixerTrackStrip.axaml`、`ExpSelector.axaml`（阶段 5 改 TemplatedControl）
- `D:\xklmy文件夹\XK\XKLMY\项目\vibe coding\UI\index.html`（阶段 0 重设计）

## 验证（每阶段）

1. `dotnet build` 全绿（TreatWarningsAsErrors 收敛）
2. `dotnet run --project OpenUtau` 无 `[ERR]` 日志
3. `dotnet test`（AppTest 是 XAML 解析回归门；ThemeContractTests 逐阶段累积）
4. **verify skill 端到端运行时验证**（用户硬性要求）
5. 阶段 0/4/6 截图对照预览库 v4.0

## 阶段闸门（用户硬性要求）

**每个阶段完成验收后、进入下一阶段前，必须先让用户预览确认**：
- 阶段 0：浏览器打开预览库 v4.0，用户逐页走查设计，确认定稿后冻结令牌值。
- 阶段 1-6：`dotnet run` 起应用，用户实机预览该阶段影响的界面（必要时附截图），确认外观/行为符合预期后才继续。
- 阶段 4（删 Fluent）：预览 = 全控件清点截图，用户确认无空白。
- 预览发现的问题在本阶段内修复后再进入下一阶段；不得带着未确认的视觉改动推进。

## 风险清单

| # | 风险 | 缓解 |
|---|---|---|
| 1 | PART 失效（删 Fluent 后 /template/ 脱靶） | PART 矩阵为验收清单；PartContractTest 自动化 `GetTemplateChild` 断言 |
| 2 | ThemeManager 断键（投影读到 null） | schema 驱动 + 缺键 `[WARN]` 不覆写旧值 + test 1 断言 |
| 3 | Fluent 删除后空白 | 阶段 1-3 Fluent 兜底全控件；阶段 4 全控件清点 + verify 截图 |
| 4 | DataGrid/ColorPicker 残留键 | 保留 NuGet 包；Phase 4 对照 Fluent.xaml 逐键补齐；test 1 覆盖 |
| 5 | 样式双重写入 | 铁律「接管即删」——每阶段从 Styles.axaml 删除被接管块，不叠加 |
| 6 | 代码画布控件拿不到新画刷 | 静态字段只加不改；PlusBrushCanvasDeep 同步接入各 Canvas；ThemeChangedEvent 重绘已有订阅模式 |
| 7 | Custom YAML 兼容性 | legacy→Plus 自动映射 + 新字段全 nullable + test 5 |
| 8 | ThemeVariant 现代化回归 | 测试裁决；兜底回退保守方案（保留 ApplyTheme） |
| 9 | MixerTrackStrip TemplatedControl 破坏布局 | 阶段 5 独立；PartContractTest + verify；不过关退回 UserControl 走令牌 |
| 10 | WindowEx 语义（ExtendClientArea/WindowDecorationMargin） | 明确不触碰 WindowEx chrome 逻辑；回归验证 PianoRoll.axaml:15 绑定 |

## 分支与提交规范

- 全部在 `plus-develop` 上开发，每阶段独立 atomic commit（中文 Conventional Commits）。
- 每提交可构建可运行；阶段边界即回滚点。
