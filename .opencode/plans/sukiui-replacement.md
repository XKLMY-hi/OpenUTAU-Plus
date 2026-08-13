# SukiUI 渐进替换计划表（2026-08-02）

**目标**：SukiUI 7.0.2-nightly 接管 UI（主题 + 窗口 + 控件 + 反馈组件），PlusTheme 退役。
**策略**：渐进 A→B→C→D，每阶段闸门 = 测试绿 + 用户实机预览确认。
**基线**：Avalonia 12.1.0 + FluentTheme（过渡期保留）+ ReactiveUI.Avalonia 14.7.1（Rx 19 线，不动）。

---

## 第 0 步：提交未提交改动 + 清理试用项目

- [ ] 提交工作树 14 个文件（亚克力全清除 + 跟随主题色渐变背景，已实机验证 + 契约测试同步）
- [ ] 删 `SemiTrial/`（已无价值）
- [ ] `SukiTrial/` 保留到阶段 B 完成（host 挂载 / FindControl 兜底参考代码），B 完删

---

## 阶段 A：主题接入（任务 #20）

**目标**：Suki 主题全局生效 + 暖灰定制 + ThemeManager 接线，与现有 UI 共存。

| # | 步骤 | 说明 |
|---|---|---|
| A1 | `OpenUtau.csproj` 加 `SukiUI 7.0.2-nightly20260801` | SukiTrial 已验证与 12.1.0 兼容 |
| A2 | `App.axaml` Styles 尾部挂 `<suki:SukiTheme />` | 后挂载覆盖 Fluent 隐式模板；PlusTheme 13 种 {x:Type} 显式模板优先级更高，继续生效 → 渐进不炸 |
| A3 | 暖灰定制：`SukiTheme.GetInstance().AddColorTheme(...)` | `new SukiColorTheme("Plus 暖灰", primary, accent)`；accent 取 `#c73a3f`，primary 取暖灰面 `#282029` 系；启动时注册 + `ChangeColorTheme` 选中 |
| A4 | `ThemeManager.Apply` 接线 SukiTheme | 末尾追加 `SukiTheme.GetInstance().ChangeBaseTheme(IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light)`；注意契约测试/后台线程调用安全（`GetInstance()` 无 Styles 环境行为先验证，不稳则判空保护） |
| A5 | 构建 + 启动验收 | 见下方闸门 |

**风险**：Avalonia 12.1.0 vs SukiUI 依赖 12.0.5 运行时差异（SukiTrial 已跑通，低风险）。

**验收**：全局控件风格变 Suki（按钮/输入框/菜单/滚动条）；暖灰 accent 生效；设置里明暗切换正常；无回归（对话框/播放器/菜单浮层）；`dotnet test` 绿（ThemeContractTests 等现有 245 基线）。
**提交**：`feat(ui): SukiUI 主题接入 — 暖灰定制 + ThemeManager 接线`（1 个原子提交）

---

## 阶段 B：窗口迁移（任务 #21）

**目标**：34 个 WindowEx 窗口全部换 SukiWindow，删 WindowDrawnDecorations 自绘边框。

### B1 WindowEx 本体改造（核心，一次改完）
- 基类 `WindowEx : Window` → `WindowEx : SukiWindow`
- 删全部 WindowDrawnDecorations 逻辑：`WindowDecorations` / `ExtendClientAreaToDecorationsHint` / `OnApplyTemplate` Presenter 绑 `WindowDecorationMargin` / TitleBar 相关 PART 字段
- SukiWindow 自带标题栏 + 按钮；背景用 `BackgroundStyle`（渐变跟随主题色，正合"跟随主题色渐变背景"决策）——`PlusBrushWindowBackground` 是否退场阶段 B 末定（Suki 渐变若与令牌一致则删，保留则由 SukiWindow.Background 显式绑定）
- **陷阱备忘**：SukiWindow 派生窗口 x:Name 字段不填充 → WindowEx 内一律 `FindControl<T>` 兜底；`TransparencyLevelHint` 固定 `None` 保留
- **测试适配**：ThemeContractTests 里 5+ 处 `new WindowEx()` + ApplyTemplate/FindPart —— 基类换 Suki 后模板 PART 名全变，逐个更新断言（ToggleSwitch/ComboBox/Button/ListBoxItem 改按 Suki 模板实测值）

### B2 34 窗口适配
- 清单：MainWindow、MixerWindow、RenderWindow、PianoRollDetachedWindow、SingersDialog、PhoneticAssistant、VstRack/VstEditorWindow、TrackEffectRack、DebugWindow、LoadingWindow、PackageManagerDialog、ThemeEditorWindow、MessageBox + 20 个对话框
- 机械改动：继承已由 WindowEx 中转（大部分窗口零改动）；各自 `.axaml` 检查：`ExtendClientArea`/`WindowDecorationMargin` 绑定（PianoRoll.axaml:15 依赖！）→ 按 SukiWindow 语义迁移
- **多窗口 Dialog/Toast 设计**：DialogHost/ToastHost 放 MainWindow（主入口），子窗口弹 SukiDialog 经 MainWindow 的 manager；单窗口对话框（独立启动路径）各自挂 host——按窗口用途分两派处理

### B3 overlay 弹窗迁移
- 已有内置 overlay（偏好设置/检查更新/退出确认）→ 迁入 `SukiWindow.Hosts` 的 DialogHost（SukiDialog 承载）或保留 OverlayCard 机制但换 Suki 模板——**B3 只迁 Hosts 容器接线，内容组件迁移放阶段 D**（避免本阶段过载）

**验收**：主窗口 + 34 子窗口全部 SukiWindow 风格（标题栏、圆角、渐变背景跟随明暗）；无 WindowDrawnDecorations 残留（grep 为零）；最小化/最大化/关闭/拖拽全正常；明暗切换全窗口联动；`dotnet test` 绿。
**提交**：拆 3-4 个原子提交（`WindowEx 基类迁移` → `对话框窗口适配` → `主窗口/工作区窗口适配` → `测试适配`）

---

## 阶段 C：PlusTheme 退役 + 主题机制统一（任务 #22）

**目标**：删光自绘模板，主题状态单一权威。

- [ ] 删 13 种 PlusTheme ControlTheme 模板（PlusTheme.axaml 或分布文件）
- [ ] `Styles.axaml` 双套残留清理（旧块：Window 背景、Fluent 兼容键覆盖等——与 Suki 重复者删，Suki 不覆盖的键并入 Plus.Resources 或迁移到 Suki 定制）
- [ ] `ThemeEditorViewModel` 33 处逐键写根旧轨道 → 统一：主题编辑结果映射为 `SukiColorTheme`（AddColorTheme + ChangeColorTheme），弃逐键 `ResourceDictionary` 覆写
- [ ] `CustomTheme.axaml` 死文件删除——**保留 `CustomTheme.ThemeYaml` + `BuildPalette` 类**（ThemeContractTests `LegacyThemeYaml_MapsToPlusKeys` 依赖），移到 `ThemeManager` 附近或 Core
- [ ] Plus.Resources 令牌层**保留**（40+ 语义键 Suki 不覆盖，供画布/特殊控件/契约测试）；ThemeManager BrushBindings 投影保留（pianoroll 画刷 Suki 无）
- [ ] ThemeManager 瘦身：Apply 内部以 SukiTheme 为基底权威，Plus* 令牌为覆写层

**验收**：全局 UI 无双重写入（模板无 PlusTheme 残留 grep 零）；自定义主题编辑功能可用（映射 SukiColorTheme）；`dotnet test` 绿（契约测试重写：Plus 键断言保留、模板 PART 断言删除/替换）。
**提交**：拆 3-4 个原子提交。

---

## 阶段 D：反馈组件（任务 #23）

**目标**：Toast/Dialog/MessageBox 全部走 Suki。

- [ ] 现有自绘 Toast + MessageBox 调用点枚举 → `FluentSukiToastBuilder`（NotificationType 在 `Avalonia.Controls.Notifications`）+ `SukiMessageBoxHost`（7.x host 式，先深挖 API）
- [ ] 确认 Suki 7.x 有无 InfoBar；无则用 SukiToast 平替
- [ ] SukiDialog 替代零散确认框（退出确认等）
- [ ] 删旧自绘反馈组件代码
- [ ] SukiTrial 删除；最终清理：SukiUI 全量接管收尾检查

**验收**：所有用户反馈路径走 Suki 组件；样式与主题联动；`dotnet test` 绿。
**提交**：拆 2-3 个原子提交。

---

## 全局风险与约束

| 风险 | 缓解 |
|---|---|
| x:Name 字段不填充（SukiWindow 派生） | 一律 FindControl 兜底（SukiTrial 实证） |
| ContractTests 依赖 WindowEx/PlusTheme PART | 阶段 B/C 同步重写测试，每阶段全量跑 |
| 对话框 20 个各自弹窗行为 | 阶段 B 内按单窗口/主窗口两派处理 DialogHost |
| 多窗口 Toast 归属 | 主窗口统一 Hosts，先验证子窗口弹 SukiDialog 可行性（SukiTrial 单窗口已验证，多窗口需实测） |
| NuGet 镜像 SukiUI nightly 可用 | SukiTrial 已成功引用，✅ |
| CompiledBindings 过渡关闭中 | 不变，Suki 模板不要求开 |

## 每阶段闸门

1. 阶段任务完成 → `dotnet build` + `dotnet test`（245 基线，StringTest 偶发重跑）
2. 提交（原子、中文 Conventional Commits、每提交可构建可运行）
3. **用户实机预览确认 → 才进下一阶段**

## 任务对应
- #20 阶段 A（in_progress）
- #21 阶段 B
- #22 阶段 C
- #23 阶段 D
