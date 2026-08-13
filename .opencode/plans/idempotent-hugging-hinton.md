# 亚克力 + 自定义标题栏实施方案

## Context

将 OpenUTAU Plus 升级为专业 DAW 外观——Windows 亚克力模糊效果 + 自绘标题栏（仅 MainWindow），其他大窗口加亚克力背景。

### 已有基础设施（可直接复用）

| 资源 | 文件 | 
|------|------|
| SplashWindow 已有 NoChrome+BorderOnly 模式 | `Views/SplashWindow.axaml` |
| DebugWindow/MixFxDialog 已有 AcrylicBlur 示例 | `Views/DebugWindow.axaml.cs`, `Views/MixFxDialog.axaml.cs` |
| 平台检测 | `Core/Util/OS.cs` — `IsWindows()` |
| 窗口状态持久化 | `MainWindow.axaml.cs` 构造函数/析构 |
| WindowDecorationMargin 绑定 | 17+ 对话框使用中 |
| Avalonia 内置 CaptionButtons 控件 | `Avalonia.Controls.Chrome.CaptionButtons` |

### 平台限制

| 平台 | 亚克力 | 自定义标题栏 |
|------|--------|------------|
| Win 11 | ✅ Mica | ✅ |
| Win 10 | ✅ AcrylicBlur | ✅ |
| macOS | ❌ 回退纯色 | ❌ 保留系统边框 |
| Linux | ❌ 回退纯色 | ❌ 保留系统边框 |

## 实施步骤

### Phase 1: 基础设施 (~2 文件)

1. **OS.cs 新增 Win11 检测** 
   ```csharp
   public static bool IsWindows11OrGreater() =>
       OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000, 0);
   ```

2. **WindowAcrylicHelper 工具类** (`OpenUtau/Views/WindowAcrylicHelper.cs`)
   - `ApplyAcrylic(Window w)` — Win11→Mica, Win10→AcrylicBlur, 其他→无操作
   - `ApplyCustomTitleBar(Window w, string title)` — 设置 NoChrome + 挂载标题栏
   - `FallbackToNormal(Window w)` — macOS/Linux 回退

### Phase 2: MainWindow 自定义标题栏 (~3 文件)

3. **MainWindow.axaml** — 窗口级属性：
   ```xml
   ExtendClientAreaToDecorationsHint="True"
   ExtendClientAreaChromeHints="NoChrome"
   Background="Transparent"
   ```
   布局变化：外层 Grid → 标题栏 Grid(高度36px) + MainGrid(移除WindowDecorationMargin)

4. **MainWindow.axaml** — 自定义标题栏行：
   ```
   [AppIcon 18x18] [Title "OpenUTAU Plus v0.1.568.2"] [menu bar] [— □ ✕ CaptionButtons]
   ```
   菜单栏集成到标题栏中（整行可拖拽移动窗口）。

5. **MainWindow.axaml.cs** — 事件处理：
   - `OnTitleBarPressed` → `BeginMoveDrag(e)`（标题栏拖拽）
   - 双击标题栏 → 最大化/还原切换
   - `OnClosed/OnMinimized` → 使用 `CaptionButtons` 内置按钮（无需手写三按钮逻辑）

### Phase 3: 弹窗亚克力 (~5 文件)

为以下弹窗在构造函数中调用 `WindowAcrylicHelper.ApplyAcrylic(this)`：
- **MixerWindow** — DAW 混音台标配毛玻璃
- **RenderWindow** — 卡片式布局，亚克力突显层次
- **PreferencesDialog** — 大面积侧边栏 + 滚动内容
- **TrackEffectRack** — 效果器架
- **VstEditorWindow** — VST 插件编辑器

弹窗**不改标题栏**，只加亚克力背景，保留系统边框和按钮。

### Phase 4: Welcome 页继承亚克力 (~0 文件)
欢迎页在 MainWindow 内部，窗口透明后自动获得亚克力背景。欢迎页卡片调整透明度（Opacity=0.92）让模糊透出。

### 待略过的窗口
- TypeInDialog / SliderDialog / MessageBox — 太小太临时
- SplashWindow / LoadingWindow — 已有自定义无边框模式
- DebugWindow / MixFxDialog — 已有 AcrylicBlur，不动

## 修改文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Core/Util/OS.cs` | 修改 | 新增 `IsWindows11OrGreater()` |
| `Views/WindowAcrylicHelper.cs` | **新增** | 亚克力+标题栏工具类 |
| `Views/MainWindow.axaml` | 修改 | 自定义标题栏行 + 窗口属性 |
| `Views/MainWindow.axaml.cs` | 修改 | 标题栏拖拽事件 + 丙烯酸初始化 |
| `Views/MixerWindow.axaml.cs` | 修改 | 构造中 ApplyAcrylic |
| `Views/RenderWindow.axaml.cs` | 修改 | 构造中 ApplyAcrylic |
| `Views/PreferencesDialog.axaml.cs` | 修改 | 构造中 ApplyAcrylic |
| `Views/TrackEffectRack.axaml.cs` | 修改 | 构造中 ApplyAcrylic |
| `Views/VstEditorWindow.axaml.cs` | 修改 | 构造中 ApplyAcrylic |

## 验证方式

1. `dotnet build` 0 错误
2. Win11 启动 → 标题栏应显示自定义标题+Mica 亚克力背景
3. Win10 启动 → AcrylicBlur 毛玻璃效果
4. 标题栏拖拽移动窗口 → 正常
5. 双击标题栏 → 最大化/还原切换
6. ─ □ ✕ 按钮 → 功能正常
7. 混音台/渲染窗口/偏好设置 → 亚克力毛玻璃背景
8. 浅色/暗色切换 → 亚克力颜色随主题变化
9. VST 插件加载/播放 → 不崩溃
