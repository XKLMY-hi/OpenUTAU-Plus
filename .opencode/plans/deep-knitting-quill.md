# OpenUTAU Plus 实施计划

## 项目目标

基于 OpenUTAU 创建分支版本，添加两个核心功能：
1. **混音台 (Mixer Panel)** — 集中的轨道混音控制面板
2. **VST/VST3 插件支持** — 识别、加载并在音频信号链中使用 VST3 效果器插件

## 技术栈

- **语言**: C# 12 (.NET 8)
- **UI 框架**: Avalonia 11.x + ReactiveUI (MVVM)
- **VST3 托管**: [OwnAudioVst](https://www.nuget.org/packages/OwnAudioVst/) (v1.6.x) — 唯一成熟的跨平台 .NET VST3 宿主库
- **音频后端**: MiniAudio (跨平台) / NAudio (Windows fallback)

---

## 前置工作：克隆并构建 OpenUTAU

```bash
git clone https://github.com/stakira/OpenUtau.git
cd OpenUtau
git checkout -b openutau-plus
dotnet restore
dotnet build
dotnet run --project OpenUtau
```

验证能正常构建和运行后，开始在分支上进行开发。

---

## Phase 1: VST3 宿主基础设施

### 1.1 添加 OwnAudioVst 依赖

在 `OpenUtau.Core.csproj` 中添加：
```xml
<PackageReference Include="OwnAudioVst" Version="1.6.*" />
```

### 1.2 创建 VST 插件管理服务 (`OpenUtau.Core/Audio/`)

新建文件结构：
```
OpenUtau.Core/Audio/
├── IVstPluginHost.cs          # VST 宿主抽象接口
├── VstPluginHost.cs           # OwnAudioVst 封装实现
├── VstPluginInfo.cs           # 插件元数据 (名称/厂商/参数/延迟等)
├── VstPluginScanner.cs        # 扫描系统 VST3 目录，发现已安装插件
├── VstEffectSlot.cs           # 效果器槽位 — 绑定一个插件实例 + 参数快照
└── VstEffectChain.cs          # 效果器链 — 多个 VstEffectSlot 串联处理音频
```

**核心设计要点**：

- `VstPluginHost` 封装 `ThreadedVst3Wrapper`，管理插件生命周期
- 音频线程调用 `ProcessAudio(float[][] inputs, float[][] outputs, int numSamples)` — 同步调用，不能 async
- 参数控制通过 `SetParameter` / `GetParameter` — 可从任意线程调用
- 插件编辑器窗口通过 `VstEditorController.OpenEditorAsync()` 打开原生窗口

### 1.3 插件扫描器

扫描标准 VST3 目录：
- **Windows**: `C:\Program Files\Common Files\VST3\`, `%LOCALAPPDATA%\Programs\Common\VST3\`
- **macOS**: `/Library/Audio/Plug-Ins/VST3/`, `~/Library/Audio/Plug-Ins/VST3/`
- **Linux**: `/usr/lib/vst3/`, `~/.vst3/`

启动时自动扫描，将发现的插件信息缓存到 `VstPluginInfo` 列表中。

### 1.4 集成到现有信号链

当前信号链：
```
WaveSources → WaveMix(part) → WaveMix(track) → Fader → WaveMix(master) → MasterAdapter → IAudioOutput
```

修改方案 — 在两个位置插入 VST 效果处理：

1. **轨道插入效果**（在 Fader 之后）：
   ```
   ... → Fader → VstEffectChain(track) → WaveMix(master) → ...
   ```

2. **主总线效果**（在 MasterAdapter 之前）：
   ```
   ... → WaveMix(master) → VstEffectChain(master) → MasterAdapter → ...
   ```

需要在 `RenderEngine.RenderMixdown()` 中创建新的 `VstInsertEffect` 类（实现 `ISignalSource` 接口），在音频回调中调用 VST 的 `ProcessAudio`。

---

## Phase 2: 混音台 UI

### 2.1 数据模型扩展 (`OpenUtau.Core/`)

扩展 `UTrack` 类：
```csharp
// 新增字段
public List<VstEffectSlot> InsertEffects { get; set; }  // 轨道插入效果链
public string Color { get; set; }                        // 轨道颜色（用于混音台识别）
```

扩展 `UProject` 类：
```csharp
public List<VstEffectSlot> MasterEffects { get; set; }   // 主总线效果链
```

序列化：使用 YamlDotNet 将效果链写入 `.ustx` 文件（存储插件路径 + 参数快照）。

### 2.2 混音台面板 UI (`OpenUtau/Views/`)

新建文件：
```
OpenUtau/Views/
├── MixerWindow.axaml          # 混音台窗口布局
├── MixerWindow.axaml.cs       # Code-behind
├── MixerChannel.axaml         # 单个混音通道控件（复用每个轨道）
├── MixerChannel.axaml.cs
└── MixerControl.axaml         # 电平表/仪表控件（音量可视化）

OpenUtau/ViewModels/
├── MixerWindowViewModel.cs    # 混音台 VM — 管理所有通道
├── MixerChannelViewModel.cs   # 单个通道 VM — 绑定到 UTrack
└── VstPluginBrowserViewModel.cs  # VST 插件浏览器 VM
```

**混音台布局设计**：

```
┌──────────────────────────────────────────────────────────────┐
│  [File] [View]                              OpenUTAU Mixer  │
├──────────────────────────────────────────────────────────────┤
│  Master  │ Track 1  │ Track 2  │ Track 3  │ ...  │  +Add   │
│  ─────── │ ──────── │ ──────── │ ──────── │      │  Track  │
│  ████    │ ████     │ ████     │ ████     │      │         │
│  ████    │ ████     │ ████     │ ████     │      │         │
│  Level   │ Level    │ Level    │ Level    │      │         │
│  ─────── │ ──────── │ ──────── │ ──────── │      │         │
│  VOL     │ VOL      │ VOL      │ VOL      │      │         │
│  [══╪══] │ [══╪══]  │ [══╪══]  │ [══╪══]  │      │         │
│  PAN     │ PAN      │ PAN      │ PAN      │      │         │
│  [══╪══] │ [══╪══]  │ [══╪══]  │ [══╪══]  │      │         │
│  ─────── │ ──────── │ ──────── │ ──────── │      │         │
│  [M] [S] │ [M] [S]  │ [M] [S]  │ [M] [S]  │      │         │
│  ─────── │ ──────── │ ──────── │ ──────── │      │         │
│  FX Slot │ FX Slot  │ FX Slot  │ FX Slot  │      │         │
│  [Reverb]│ [Empty]  │ [Delay]  │ [Empty]  │      │         │
│  [+Add]  │ [+Add]   │ [+Add]   │ [+Add]   │      │         │
│  ─────── │ ──────── │ ──────── │ ──────── │      │         │
│  │███    │ │███     │ │█       │ │███     │      │         │
│  │███    │ │███     │ │█       │ │███     │      │         │
└──────────────────────────────────────────────────────────────┘
```

**每个通道包含**：
- 电平表（实时音频电平可视化）
- 音量推子（复用现有 `track.Volume`，范围 -24dB ~ +12dB）
- 声像旋钮（复用现有 `track.Pan`，范围 -100L ~ +100R）
- Mute / Solo 按钮（复用现有 `track.Mute` / `track.Solo`）
- 效果器槽位列表（最多 4 个插入效果器）
- 轨道名称标签

**主通道 (Master)**：
- 主音量推子
- 主电平表
- 主总线效果器槽位

### 2.3 VST 插件浏览器

点击效果器槽位的 `[+Add]` 按钮时弹出：
- 显示已扫描的所有 VST3 插件列表
- 按类别/厂商筛选
- 搜索框
- 双击加载到对应槽位

### 2.4 集成到主窗口

- 在菜单栏添加 `View → Mixer` 菜单项
- 混音台作为可停靠/浮动窗口打开
- 与现有的 TrackHeader 控件双向同步（混音台修改音量 → TrackHeader 同步更新，反之亦然）

---

## Phase 3: 实时音频处理

### 3.1 创建 VstInsertEffect (实现 ISignalSource)

在 `OpenUtau.Core/Audio/VstInsertEffect.cs`：

```csharp
public class VstInsertEffect : ISignalSource
{
    private readonly ThreadedVst3Wrapper _plugin;
    private float[][] _tempBuffer;
    
    public bool IsReady(int pos, int duration) => _plugin.IsReady;
    
    public int Mix(int pos, int duration, float[] buffer, int offset, int sampleRate)
    {
        // 1. 复制 buffer 到 _tempBuffer (float[][] 格式)
        // 2. 调用 _plugin.ProcessAudio(_tempBuffer, _tempBuffer, 2, duration)
        // 3. 复制 _tempBuffer 回到 buffer
        return duration;
    }
}
```

`ISignalSource` 是 OpenUTAU 现有的音频源接口 — `WaveSource`, `WaveMix`, `Fader` 都实现它。我们的效果器也实现这个接口，可以无缝插入信号链。

### 3.2 延迟补偿

VST 插件可能有处理延迟（Plugin Delay Compensation, PDC）。`VstEffectChain` 需要：
- 查询每个插件的延迟样本数
- 总延迟 = 所有插入效果延迟之和
- 在 `MasterAdapter` 中相应调整总输出对齐

### 3.3 性能考虑

- **预渲染**：保持 OpenUTAU 现有的预渲染缓存机制，效果器在预渲染阶段离线处理（非实时）
- **实时播放**：效果器在音频线程运行，保持低延迟
- **旁路 (Bypass)**：不用的效果器槽位可以旁路跳过，减少 CPU 消耗

---

## Phase 4: 序列化与持久化

### 4.1 USTX 格式扩展

在 `.ustx` YAML 文件中添加效果器配置：

```yaml
tracks:
  - name: "Track 1"
    volume: 0.0
    pan: 0.0
    mute: false
    solo: false
    effects:                          # 新增
      - plugin_path: "C:/.../reverb.vst3"
        parameters:
          - id: 0
            value: 0.5
          - id: 1
            value: 0.3
        bypass: false
      - plugin_path: ""               # 空槽位
        parameters: []
        bypass: true
```

### 4.2 跨平台兼容

- 插件路径使用变量/相对路径（VST3 插件 ID 作为主标识符）
- 如果某平台不存在已配置的插件，跳过并显示警告，不阻止项目加载

---

## Phase 5: 测试与验证

### 5.1 功能测试

1. 启动应用 → 确认 `View → Mixer` 菜单存在
2. 打开混音台 → 确认所有轨道通道显示
3. 调整音量推子 → 确认 TrackHeader 同步更新
4. 扫描 VST3 插件 → 确认系统已安装的 VST3 出现在浏览器中
5. 加载 VST3 效果器到轨道 → 确认播放时能听到效果
6. 打开效果器原生编辑器 → 确认窗口正常显示
7. 保存/加载项目 → 确认效果器配置正确恢复

### 5.2 边界情况

- 无 VST3 插件安装时的优雅降级
- 加载不存在的插件路径
- 插件崩溃不影响主应用

---

## 关键风险与缓解

| 风险 | 缓解措施 |
|------|---------|
| OwnAudioVst 在 Linux 上的兼容性问题 | 先聚焦 Windows 开发，Linux 后续适配 |
| VST 音频回调中的性能瓶颈 | 限制每轨道 4 个插入槽位，支持旁路 |
| 用户不熟悉 C# / Avalonia | 每个 Phase 提供详细步骤，逐步构建 |
| OwnAudioVst API 变更 | 锁定主版本号，通过 IVstPluginHost 接口隔离 |

---

## 开发顺序

1. **前置工作** — 克隆、构建、验证原版 OpenUTAU
2. **Phase 1** — VST3 宿主基础设施（无 UI，先跑通加载和处理音频）
3. **Phase 2** — 混音台 UI（先用模拟数据，不连 VST）
4. **Phase 3** — 将 Phase 1 的 VST 处理接入 Phase 2 的混音台
5. **Phase 4** — 序列化/持久化
6. **Phase 5** — 测试、修复、打磨
