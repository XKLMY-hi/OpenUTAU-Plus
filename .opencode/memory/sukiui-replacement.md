---
name: sukiui-replacement
description: SukiUI 替换现有 UI — 阶段 A 已收官、菜单体系/玻璃浮层踩坑、阶段 B 计划
metadata:
  node_type: memory
  type: project
  originSessionId: 84d7e7cc-b226-48d1-935d-a3e22b84ee0a
---

# SukiUI 替换计划（2026-08-02 启动，阶段 A-D 已全部收官）

## 用户决策（2026-08-02 拍板）
1. **SukiWindow 全接管** —— 窗口标题栏/背景也换 Suki（放弃刚做完的 WindowDrawnDecorations 官方边框）
2. **PlusTheme 13 种自绘控件模板退役** —— Suki 全部接管
3. **渐进式替换**（吸取"全局替换翻车"教训）：A 主题接入 → B 窗口迁移 → C PlusTheme 退役+主题机制统一 → D 反馈组件，每阶段验收
4. **玻璃浮层回归（2026-08-02 实机后）**：对话框/弹出卡片用 Suki 半透明+背景模糊质感（主内容 BlurEffect + PlusDialogCard 半透明卡）；**内容面仍全实色**
5. **音符属性面板透明化（B1 后）**：面板不再自绘背景，透明露出窗口渐变背景

## UI 库调研结论（2026-08-01 完成）
| 库 | Avalonia 依赖 | 结论 |
|---|---|---|
| **SukiUI 7.0.2-nightly** | 12.0.5 ✅ 今天还在发版 | **选中**（主题+丰富控件+自绘窗口） |
| Semi.Avalonia 12.1.0 | 12.1.0 ✅ | **纯主题包**（零自定义控件）→ 弃 |
| Material.Avalonia | 12.0.0 仅 nightly | 风格不合弃 |
| Citrus.Avalonia | 11.0.0 ❌ | 出局 |
| FluentAvalonia | 停止维护 | 出局 |

## SukiUI 7.x API 关键知识（踩坑实证）
- **主题挂载**：`<suki:SukiTheme />` 直接放 Application.Styles（SukiTheme 自身是 IStyle；旧版 avares StyleInclude 已废弃）
- **主题切换**：`SukiTheme.GetInstance()` → `ChangeBaseTheme(ThemeVariant)` / `ChangeColorTheme(SukiColor|SukiColorTheme)` / `AddColorTheme(new SukiColorTheme("名", primary, accent))`（按 DisplayName 去重，重复 Add 抛 "already been added"）
- **⚠️ ChangeBaseTheme 会重置 ActiveColorTheme 为默认** → 顺序铁律：先明暗后色彩
- **SukiWindow.Hosts**：DialogHost/ToastHost 必须放 `SukiWindow.Hosts` 容器（窗口级覆盖层），放 Content 里会崩/无效
- **⚠️ x:Name 字段坑**：SukiWindow 派生窗口在运行时加载路径下 x:Name 字段不填充（编译产物有字段但运行时 null），必须 `FindControl<T>("Name")` 兜底（SukiTrial 实证）
- **Manager 手动挂载**：7.x host 不自动创建 manager：`DialogHost.Manager = new SukiDialogManager(); ToastHost.Manager = new SukiToastManager();`
- **Toast**：`FluentSukiToastBuilder.CreateSimpleInfoToast(ToastHost.Manager).WithTitle().WithContent().OfType(NotificationType.Success)`（NotificationType 在 **Avalonia.Controls.Notifications**）
- **SukiDialog**：属性是 `ActionButtons`（不是 Buttons）；`DialogHost.Manager.TryShowDialog(dialog)`
- **SukiMessageBox 7.x 是 host 式**（SukiMessageBoxHost + ShowDialogResult），API 与 6.x 不同，未深挖
- **按钮变体**：`Classes="basic/flat/accent/danger/warning/success/information/outlined/rounded/card/large"`（SukiButtonStyles 枚举）
- **TextBoxExtensions**：Unit/ResetText/Error/Prefix 附加属性（`SukiUI.Theme` 命名空间）
- **TextIcon 7.x 已移除**（用 SukiUI.Content.Icons 静态类）
- **SukiWindow 背景**：BackgroundStyle 枚举 Gradient/GradientSoft/GradientDarker/Flat/Bubble + 跟随主题色
- **Avalonia 12 移除 ProgressRing**（Suki/Semi 都无环形进度替代；Suki 有 CircleProgressBar）
- **Avalonia 12 改名**：MenuItem 属性 IsSubmenuOpen → **IsSubMenuOpen**；PlacementMode 移除 RightEdgeAlignedLeft（用 RightEdgeAlignedTop）；DoubleTransition 无 From/To（TransitionBase 重构，无 KeyFrame 动画类型）
- **控件接管实测**：{x:Type} ControlTheme 按 Styles 顺序后者胜（Suki 后挂覆盖 PlusTheme/Fluent）；Suki 7.x **无 TextBox 模板**（TextBoxExtensions 仅附加属性，TextBox 仍走 PlusTheme）
- **BlurEffect**：`Avalonia.Media.BlurEffect`（非 Media.Effects），`Visual.Effect` 属性挂元素上模糊自身渲染

## 紧凑菜单体系（SukiCompactMenu.axaml，阶段 A 产出——探针实证踩坑合集）
- Suki 菜单条模板高 45 + MenuItem 内容区固定 ~10px → 24px 顶栏文字半切 → 自建紧凑模板（行高 ~25px）
- **Menu 模板 = 纯 ItemsPresenter**（Popup 内 ItemsPresenter 无条件挂载宿主 Items → 与菜单条双重挂载崩溃）
- **MenuItem 逻辑按层级设 Popup Placement**（顶层 BottomEdgeAlignedLeft / 子级 RightEdgeAlignedTop），前提模板 Popup 不硬编码；但右键一级（ContextMenu 子项）逻辑误判顶层给 Bottom → 默认 MenuItem 模板硬编码 RightEdgeAlignedTop；顶栏一级用 MenuItemTopLevel 模板（无 Placement）+ `Menu.ItemContainerTheme` 指定容器模板
- **Popup 内 TemplateBinding Items 会崩 headless**（独立命名空间）→ 内容用 Border+ItemsPresenter（自动绑宿主 Items）
- **Transitions 对象放资源字典 → 加载时实例化崩** → 动画用 Style.Animations+KeyFrame 旧语法
- 弹出动画：Popup 内容 Border Classes=menuPopup + Style.Animations Opacity 淡入 160ms
- **幽灵键**：PlusBrushSurfaceOverlay 被 4 处引用却从未定义（菜单浮层透明 bug）→ Brushes.axaml 补

## 阶段 A 已完成（提交 a6c8ea11 → 537671ed，测试 246/247，全绿）
- 主题接入（App.axaml SukiTheme + ThemeManager.ApplySukiTheme 暖灰 #c73a3f）
- SukiOverrides.axaml 收敛层（HarmonyOS 13px / Button Padding 12,5 / TextBlock+Label+Expander Foreground 显式绑 TextFillColorPrimaryBrush——**Suki 覆盖 Fluent 默认值导致暗色下黑字**；Brushes.axaml 补 TextFill* 键，Resources 层优先于 Styles 层）
- 紧凑菜单 + 退出确认 Suki 玻璃卡片（OverlayLayer 显示时 MainGrid.Effect=BlurEffect(Radius 24) + 卡片 PlusDialogCard 暗 #49aaaaaa/亮 #fdfefefe + 淡入动画 Animation.RunAsync；关闭路径两处移除 Effect）
- 测试污染修复：AppTest.StringsTest 改 [AvaloniaFact]（手动 SetupWithoutStarting 残留静态 Dispatcher/MediaContext 污染后续 AvaloniaFact 全量顺序）——StringsTest 从此不偶发

## 阶段 B1 已完成（f3b23f52 → ae5a1e5c 共 3 提交，测试 252/253 全绿）
- **B1a 基类改造**（f3b23f52）：`WindowEx : Window` → `WindowEx : SukiWindow`；删自绘边框三源（WindowEx.cs 构造函数 ExtendClientArea/WindowDecorations + OnApplyTemplate Presenter 订阅、Styles.axaml controls|WindowEx Style、PlusTheme.axaml 摘除 Plus.Window.axaml ResourceInclude）；LoadingWindow 清 ExtendClientAreaTitleBarHeightHint；TrackEffectRack 内联 `new WindowEx(bool)` 两处改无参（**false 模式语义消失**，统一 Suki 装饰）；SplashWindow 保持普通 Window 不动（启动徽标绕开）
- **🔑 重大简化发现：34 个窗口 xaml 根元素与派生类声明零改动**——xaml 根 `<controls:WindowEx>` 类型兼容 SukiWindow，装饰经基类继承自动生效（只有 LoadingWindow 删一行残留）
- **🔑 x:Name 坑实证排除**：编译 XAML 路径（标准 InitializeComponent）下 SukiWindow 派生窗口 x:Name 字段正常填充（WindowSukiProbeTests 反射断言 MessageBox 的 Text/TextPanel/Buttons 非 null）——**SukiTrial 的 null 坑仅存在于手动 AvaloniaXamlLoader.Load(this) 运行时加载路径**，OpenUtau 全部窗口无需 FindControl 兜底
- **TransparencyLevelHint=None 保留**：SukiWindow 圆角/阴影实机无问题（用户未抱怨）
- **顶部留白**（8a0cec9b）：主窗口根 Grid→Border Padding=0,8,0,0 + 内包 Grid（Border.Child 只能一个，而根 Grid 有 DockPanel+OverlayLayer 两个子元素 → 必须 Border>Grid 包装）
- **音符属性面板透明**（ae5a1e5c）：B1 后 SukiWindow 接管窗口背景，面板 PlusBrushWindowBackground 微渐变（#1e1e28→#282029 色差极小）与 Suki 渐变不衔接视觉呈纯色 → 删 Background 改透明露出窗口渐变；新增 GradientBrushProbeTests（Default/Dark 变体下画刷 GradientStop 解析渲染正常，防回归）

## ⚠️ B1 崩溃排查经验（重要踩坑）
- **构建失败会产出损坏 dll**：Avalonia 12 打包器（GenerateAvaloniaResourcesTask）会把显式 AvaloniaResource + AvaloniaXaml 的 axaml 各打一次（obj/Avalonia/resources 中间产物双份是正常的），**CompileAvaloniaXamlTask（XamlIl 编译）会去重成单份**；但**构建失败（如 AVLN3000）时 XamlIl 中止，双份中间产物被直接嵌入 dll → 启动 AssemblyDescriptor "An item with the same key has already been added. Key: /Assets/Icons.axaml" 崩溃**
- **教训：失败构建后必须完整重建再启动，绝不用 --no-build 跑旧 dll**；排查时一度误判为 Avalonia 12 打包 bug（csproj Exclude/Remove 实验均无效）——真正根因是失败构建的损坏产物
- 排查工具链：`!AvaloniaResources` 是聚合 manifest 资源（GetManifestResourceNames 可见）；obj/Avalonia/resources 是打包中间产物（二进制 URI 清单）；dll 内嵌单份=正常、双份=损坏

## 阶段 B3/B4 已完成（3a25159a → c16aba77，测试 257/258 全绿）
- **B3 偏好设置 → SettingsLayout**（3a25159a）：10 分类映射 SettingsLayoutItem；**四个上游坑连环踩**：
  1. **Items 是只读 DirectProperty**（`RegisterDirect` 无 setter，内部字段 `_bounds` 默认 null，模板应用前 getter 返回 null）——XamlIl 对 `<SettingsLayout.Items>` 属性元素生成 **getter().Add()**（不调 setter）→ populate NRE。**解法：item 声明在隐藏容器 Grid（保留 x:Name/事件绑定），构造后 `PrefsLayout.Items = 隐藏容器.Children.Cast<SettingsLayoutItem>().ToArray()`**（setter → SetAndRaise 写 _bounds）
  2. **ControlTheme 查找不沿继承链**：派生适配类（new getter 惰性 List）会丢模板（UpdateItems 的 GetTemplateChildren().First 崩）——**必须用 base SettingsLayout 类型**，不能继承包装
  3. **MinWidthWhetherStackSummaryShow 默认 1100px**：浮层（60% 主窗口）宽度不足时左侧导航折叠（Width 动画到 0）——**设 100 强制显示**；StackSummaryWidth 400→170
  4. **MenuChip 用 Roboto 覆盖全局字体**——**需显式 FontFamily={DynamicResource PlusFontFamily}**（RadioButton + 内部 TextBlock 双设，防 Suki 样式覆盖继承）；圆点装饰 Ellipse 隐藏、文字 12px 居中、高度 20
- **XamlIl 集合填充证据链**：反编译 `!XamlIlPopulate`（ilspycmd，装过全局工具）实证 getter().Add()；**SettingsLayoutItem 子元素直接写内容触发 ResolveContentPropertyTransformer 编译崩溃** → 必须显式 `<SettingsLayoutItem.Content>` 属性元素
- **B4 DialogHost/ToastHost**（0cc6f529）：MainWindow 根元素 `<controls:WindowEx.Hosts>` 挂 SukiDialogHost/SukiToastHost + 构造函数手动 Manager（SukiDialogManager 在 **SukiUI.Dialogs**、SukiToastManager 在 **SukiUI.Toasts**）；SukiTrial 的 FindControl 兜底不需要（编译路径 x:Name 正常）
- **对话框卡片**（c16aba77）：PlusDialogCard 暗色 `#49aaaaaa`（灰玻璃）→ `#B31E1E28`（窗口渐变 #1e1e28 同色系深色玻璃 ~70%）；用户"背景应该是渐变深色不是灰色"
- 实机验证闭环：每轮改 axaml → **完整构建**（注意 `dotnet build | grep | head` 管道会截断构建！head 退出 0 让 && 继续 → --no-build 跑旧 dll → 用户看到"没变化"——**构建输出重定向到文件再查**）→ 重启 → 用户截图 → image-recognize 识别（**GLM-4V 经常 429 限流，重试**）
- SukiTrial 已删（阶段 B 完删）

## 阶段 E 已完成（32c26564 → 18cf46e4，布局重构 + 侧栏素材库收官）
- **🔑 DWM 边框移除（722597bc）**：SukiWindow 透明圆角窗口的边缘"黑色边框"= **Windows DWM 系统边框**。解法（P/Invoke DwmSetWindowAttribute 三属性）：DWMWA_WINDOW_CORNER_PREFERENCE=33 DONOTROUND（关系统圆角）+ DWMWA_NCRENDERING_POLICY=2 DWMNCRP_DISABLED（关非客户区渲染）+ DWMWA_BORDER_COLOR=34 透明（Win11 边框色）。**透明 + ExtendClientArea 在 Win10 实际可行**（曾误判互斥）；WindowManagerAddShadowHint 在 Avalonia 12 仅存在于 ContextMenu
- **🔑 欢迎窗口独立化（850928be）**：Splash → WelcomeWindow（工程管理）→ 新建/打开后创建 MainWindow 接管 desktop.MainWindow；MainWindow Carousel/欢迎页/Page 属性全删；VM.InitProject 只留恢复状态；WelcomeWindow 用独立 MainWindowViewModel 实例（RecentFiles/TemplateFiles 改 public）
- **用户沟通教训**：UI 问题描述多轮拉锯（"边框太厚"实指 DWM 系统边框，非 Suki 描边/圆角）——**先确认用户所指对象再动手**，必要时 web 查证平台行为
- **配色决策（2026-08-03，5 文件同步：DarkTheme/LightTheme/CustomTheme/Plus.Resources/ThemeManager）**：
  - 背景混合：Suki **GradientDarker**（选型对比预览确认；预览环境 SukiBackground GPU 合成不渲染——预览窗口背景透明，**背景混合预览只能主程序实机**）
  - 主题色 **#B0C4DE**（LightSteelBlue）系：hover #BED4EE、pressed #9BABC1、半透明 #1F/33/4DB0C4DE
  - PlusSurfaceHover/Pressed：**#4682B4**（钢蓝，用户指定）；PlusSurfaceRaised：**#22222c**（用户改深后又从钢蓝改回）
  - 收尾提交 c7b4c14b（含 docs/SIDEBAR-LIBRARY-PLAN.md `git add -f` 入库——docs/ 在 .gitignore）
- **E4 侧栏素材库已完成（db905a81 → 18cf46e4，6 提交）**：见 [[sidebar-library-plan]]
  - E4-1：Preferences.SampleSearchPaths（List，仿 VstScanPaths）+ PathManager.SamplesPath/SamplesPaths（默认 DataPath/Samples + 用户路径合并）+ PreferencesDialog "伴奏库路径"项（仿 AdditionalSingersPath 行）
  - E4-2：SidebarViewModel（SingerItem 包装：懒加载头像 + 铭牌仅 Classic/DiffSinger 0x1/0x5；SampleItem{Name,Path}；ICmdSubscriber 订阅 SingersRefreshedNotification；扫描仅枚举文件名 wav/mp3/ogg/opus/flac；HasSingers/HasSamples 空状态标记——**Avalonia 绑定 int→bool 无隐式转换，空状态用 [Reactive] bool 而非 Count**）
  - E4-3：MainWindowViewModel.AddSingerTrack 命令链（StartUndoGroup → AddTrackCommand → TrackChangeSingerCommand → 音素器 SingerPhonemizers 钉住项 → singer.DefaultPhonemizer → TrackChangePhonemizerCommand → renderer）——复刻 TrackHeaderViewModel.ApplySingerToTrack:255-279；TracksViewModel.OnNext 自动把新轨加入列表并广播刷新 ✓
  - E4-4：SingersPanel ListBox（圆角矩形头像 34px CornerRadius 8 + LocalizedName + 铭牌 Border IsVisible=ShowBadge）；双击 DoubleTapped → AddSingerTrack；刷新按钮三段式（Loading → SearchAllSingers → SingersRefreshedNotification）
  - E4-5：SamplesPanel ListBox（icon-music 方块 + 文件名 + 试听按钮 PlaybackManager.Inst.PlayFile + 重扫按钮）；双击 → ImportAudio；**zh-CN sidebar.samples 改"采样"→"伴奏"**
  - **🔑 Avalonia 12 拖拽 API 全面改版**（旧 DataObject/DoDragDrop(e,data,effects) 全删）：发起 `new DataTransfer()` + `data.Add(DataTransferItem.Create(format, value))` + `await DragDrop.DoDragDropAsync(PointerPressedEventArgs, data, effects)`（**必须 PointerPressedEventArgs——PointerMoved 拿不到，PointerPressed 时存事件参数字段**）；自定义格式 `DataFormat.CreateInProcessFormat<T>("name")`（静态字段缓存）；接收 `args.DataTransfer.TryGetValue(format)` 返回 T?；DataTransfer.Items 是 IReadOnlyList（用实例方法 Add）；Point 无 Length（Vector 已删，手算 Math.Abs(delta.X/Y)）。拖拽挂载：窗口级 AddHandler(DragDrop.DropEvent) + OnDrop 开头先查自定义格式再走原文件逻辑
- **待办（2026-08-12 已全部清空，UI 改造结束）**：欢迎页 Suki 化已完成；VST 侧栏选项卡（i18n 名"乐器"）已就位；无遗留 UI 待办
- **用户拍板方案（2026-08-03）**：现代 DAW 工作台——顶栏（文字菜单+图标动作）+ 播放条 + 可折叠侧栏（208px，项目/素材库双选项卡 + 歌手/采样/VST 子选项卡）+ 编辑区（轨道头 300 保持）+ 状态条
- **E2 已提交**（32c26564 → d96d4a02）：单行顶栏（菜单/播放控制/动作合一，覆盖层方案真居中）、侧栏折叠浮出打开按钮、播放按钮无背景统一 12px、状态条合并；splitter 行号索引同步（UpdateResizeTooltip Row[1]/[3]、OnMixerSplitterPressed Row[5]——**编辑页网格 6 行：24/轨道区/6/卷帘 3*/6/混音器**）
- **🔑 PART_Popup 坑（603ab9c7）**：MenuItem 类靠模板 `PART_Popup` 关联子菜单 Popup 判断 IsPointerOverSubMenu（鼠标移入子菜单不关闭）。**自定义 MenuItem 模板的 Popup 必须命名 PART_Popup**，否则 _popup=null → 二级菜单悬浮即关（TrackHeader 歌手/音素器菜单点不到、顶栏子菜单同样）。headless 无法复现（无窗口模板 Popup 打不开，"no overlay layer"——Avalonia 12 的 Popup 挂 VisualLayerManager.PopupOverlayLayer，headless 环境差异）
- **🔑 图标库路线**（d96d4a02）：Heroicons → **Phosphor**（MIT，54 枚 fill 变体，256px 网格，key 不变调用点零改动）。候选调研：Lucide 无线条 fill 变体淘汰；Material Symbols 仅 Apache；MingCute Apache；**用户拍板 MIT 优先 → Phosphor**。下载源：unpkg（gstatic 可达、GitHub 不可达）；Phosphor 映射要点：home→house、save→floppy-disk、settings→gear、volume→speaker、undo/redo→arrow-counter-clockwise/clockwise、refresh→arrows-clockwise、external-link→arrow-square-out、zoom→magnifying-glass-plus/minus
- **UIPreview 保留**（布局迭代预览用；侧栏样式副本在 UIPreview/MainWindow.axaml，改样式需同步）

## 阶段 D 已完成（f7ca27dc，测试 259/260 全绿）
- **MessageBox 门面化**：自研 MessageBox 窗口（MessageBox.axaml 删除）→ SukiMessageBox 渲染；60+ 调用点签名零改动（同步门面返回 Task，避免 CS4014 波及相关 async 调用点——**CS4014 只在调用 async 方法时触发**，旧实现同步返回 Task 不触发）
- **SukiMessageBox 7.x API 全集（实证）**：
  - 类型：`SukiMessageBox`（SukiUI.MessageBox 静态类）、`SukiMessageBoxHost`（SukiUI.Controls，HeaderedContentControl）、`SukiMessageBoxButtonsFactory`/`IconsFactory`（SukiUI.MessageBox）、`SukiMessageBoxOptions`（record，MinWidth=400/MinHeight=150/SizeToContent=WidthAndHeight/CenterScreen）
  - 预设：`ShowDialogResult(owner, message, buttons, title, header, icon, options)` → Task&lt;SukiMessageBoxResult&gt;（枚举 OK/Yes/No/Cancel/Apply/Ignore/Retry/Abort/Continue/Close；按钮枚举含 OK/OKCancel/YesNo/YesNoCancel 等）
  - host 式：`ShowDialog(owner, host, title)` → Task&lt;object?&gt;（按钮 Tag 携带 SukiMessageBoxButtonTag{Result, Owner}，点击 → Owner.Close(Result)；**自定义按钮必须给 SukiMessageBoxButtonTag 否则点击不关窗**；Header/Content 为 string 时自动包装 SelectableTextBlock；ESC→Close）
  - **窗口不暴露**：主动关闭需在 host.Content 控件 AttachedToVisualTree 后 `TopLevel.GetTopLevel(content)` 捕获内部 SukiWindow，Closed 事件置空引用实现幂等 Close
  - **GetTopLevel 是静态方法**（TopLevel.GetTopLevel），不是扩展方法；Window 无 IsClosed 属性
- **链接按钮**：`Button.linkButton` 样式迁入 Styles.axaml（AccentBrush1 下划线 + pointerover AccentBrush2），错误详情 URL 化复用
- **探针**：MessageBoxFacadeProbeTests（ButtonsFactory Tag 契约 + host 组装）；WindowSukiProbeTests 的 MessageBox 实证换 LoadingWindow
- **Toast/InfoBar 不迁移**（用户拍板）：DialogHost/ToastHost 基座已就绪（B4），但无现成"非阻塞通知"调用场景——渲染错误等全是必须立即处理的阻塞弹窗，强行改造会改交互语义

## 阶段 C 已完成（651e4b40 → 0c439611，测试 257/258 全绿）
- **PlusTheme 退役 12 个被 Suki 接管的 ControlTheme**（Button/ToggleButton/ComboBox/ComboBoxItem/ToggleSwitch/CheckBox/RadioButton/Slider/ProgressBar/ListBoxItem/MenuFlyoutPresenter/ToolTip）——Suki 后挂 {x:Type} ControlTheme 一直生效，删的是死代码；实机验证无视觉回归
- **TextBox 并入 SukiOverrides.axaml Styles.Resources**（Suki 7.x 无 TextBox 模板）——PlusTheme.axaml/Plus.TextBox.axaml 删除，App.axaml 摘挂载；Themes/ 仅剩 Plus.Resources.axaml 令牌层；SukiTrial 删除
- **ThemeEditorWindow 启动崩溃**：曾由用户实机发现（暂缓修复、后续重构该工具），**2026-08-12 用户确认已解决**，无遗留

## 试用项目
- `SukiTrial/`：仓库内独立项目（不进 OpenUtau.sln），阶段 B 完删
- SukiTrial 启动：`dotnet run --project SukiTrial`（含 Manager 修复 + FindControl 兜底，可直接参考）

## 相关
- 亚克力清除+渐变背景见 [[project-overview]]；Rx 6.x 坑见 [[avalonia-12-upgrade]]
