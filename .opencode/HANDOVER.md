# 会话交接（2026-09-18）

> 恢复会话：按顺序读 `.opencode/memory/MEMORY.md` → `.opencode/memory/metronome-piano-port.md` → `.opencode/memory/env-refresh-2026-09-g-drive.md` → `.opencode/plans/upstream-piano-metronome-port.md`，然后从下方"下一步"继续。

## 状态一句话

环境重启后构建链已重建（G 盘、系统 SDK、离线 NuGet）；钢琴窗批次 A+B（A1-A10、B1/B2）全部完成；**合成/渲染管线已完成最小解耦（接缝显式化，为换上游渲染架构铺路）**；测试基线 284 → **308**；全部已推送 origin/plus-develop。

## 本次会话提交（均已推送）

```
88944390 refactor(audio): 合成/渲染管线解耦 — RenderEngine 静态门面 + RenderGate 归位音频层
d3afb1ed feat(pianoroll): 曲线编辑工具扩展 — 直线/伸缩/移动 + UCurve.ReplaceRange（B2）
b0d001c0 feat(pianoroll): Alt 拖拽复制音符（B1）
ff2836ec feat(pianoroll): 音轨区高亮钢琴窗当前显示范围（A10）
bdcaf9b4 feat(pianoroll): 音符悬停光晕改进（A9）
2954a723 feat(pianoroll): 播放音符弹跳（A8）
c8532db6 test(vst): RenderGate 并发用例改为基线相对断言
230d70ca / 41865f89 / 7379a170 docs(handover): 交接与记忆更新
```

（A8-A10、B1/B2 仍待用户实机验收；接缝重构行为零变化，但**播放/导出建议再跑一次实机确认**）

## 接缝与后续路线

详见 `.opencode/plans/audio-pipeline-seam.md`：
- 合成层对外 = `RenderEngine` 六个静态门面；运输/导出层不得持有合成内部状态（14 条契约测试锁定）
- 下一层耦合（换合成实现前必须处理）：`MasterAdapter.Waited` 搬到运输层、VST 延迟销毁安全点保留
- 之后：A/B 音频比对 → 正式引入上游 `MixPlanner`/`SampleSlot`/`RenderPriority`；四条忠实行为回归清单见该文档

## 环境变更（重要，上一次会话的命令已失效）

- `E:\tools\dotnet`（便携 SDK 9）与 `E:\home\.nuget\packages` **已不存在**；现在用系统 `C:\Program Files\dotnet`（SDK 8/9/10，默认 10.0.400）+ `C:\Users\XKLMY\.nuget\packages`
- 仍然**无外网**：restore 必须 `--ignore-failed-sources`；SDK 10 直接编 net8.0 解决方案成功（无需 global.json、无需 DOTNET_ROOT）
- 上一次会话遗留的 `obj/**` 全部只读（`Access to the path ... is denied`，ACL 正常）→ 已整体删除重建；以后遇到同类报错照此办理
- 命令与坑详见记忆 [[env-refresh-2026-09-g-drive]]

```powershell
dotnet restore OpenUtau.sln -m:1 -p:TreatWarningsAsErrors=false --ignore-failed-sources
dotnet build OpenUtau.sln --no-restore -m:1 -p:RuntimeIdentifiers= -p:UsedAvaloniaProducts=
dotnet test OpenUtau.Test\OpenUtau.Test.csproj --no-build
.\OpenUtau\bin\Debug\net8.0-windows\OpenUtau.exe
```

## 下一步：定向移植已完成，等验收后再决策

1. 先等用户实机验收 A8/A9/A10/B1/B2（清单见下）
2. 之后可选：(a) 继续盯上游新的钢琴窗/编辑类提交做定向移植；(b) 转入全量合并评估（upstream 已领先 130+ 提交，音频架构冲突需决策，见计划文档"后置"节）
3. 每步：构建 0 错误 + 测试全绿 → 用户实机预览（UI 不可自动交互）

## 待用户实机确认（A8 + A9 + A10 + B1 + B2）

A8 弹跳（「偏好设置 → 外观 → 播放时音符弹跳」，默认关）：
- 播放头刚进入某音符时，该音符上跳 0.25 秒（半正弦弧，最大 12px 或轨道高 40%），随后落回原处
- 与该音符的「播放时高亮」相互独立（可单独开关）；关闭弹跳后行为与之前完全一致

A9 悬停光晕改进：
- 光标悬停在音符上时光晕颜色 = 该音符自身颜色（选中/错误音符各随其色），不再是固定主题色
- 仅在光标/画笔/橡皮/刻刀工具或按住 Ctrl 时出光晕；音高类工具下不出
- 按住左键拖拽时不再残留光晕

A10 音轨区显示范围高亮：
- 音轨区里「钢琴窗当前打开的那个片段」上，出现一个白色半透明圆角框，标示钢琴窗当前可见的横向区间
- 在钢琴窗里横向滚动/缩放，该框实时跟随；切换到别的片段时框移动到新片段
- 空片段（没有音符）也能显示框；浅色主题下白框对比度可能偏低，若看不清请告知（可改主题令牌）

B1 Alt 拖拽复制音符：
- 按住 Alt 拖动音符 = 复制一份并拖动（原音符留在原地）；撤销名显示「复制音符」
- 多选后 Alt 拖拽 = 整组复制；普通拖拽（不按 Alt）仍是移动

B2 曲线编辑工具扩展（钢琴窗左侧曲线工具栏，需先显示表情/曲线）：
- 工具从 3 个扩展为 8 个：光标 / 画笔 / 直线 / 橡皮 / 垂直伸缩 / 水平伸缩 / 垂直移动 / 水平移动（悬停有中文提示）
- 用光标工具框选一段曲线后，切到四种变换工具可拖动选区：伸缩以选区中心为基准，移动按刻度/数值平移；右键重置
- 直线工具：左键拖动画直线；「偏好设置 → 高级 → 默认吸附曲线」默认开，关掉后曲线编辑不吸附网格

## 未决/风险

- VST 链路仍未实测（本机无可用 VST；用户已同意跳过，待重构）
- A1 关闭按钮位置是 Plus 适配决策（工具栏右端而非上游右上角），用户尚未实机看，可能需调整
- 构建异常后若报 `Key: /Assets/Icons.axaml` 重复 → 删 `OpenUtau/obj` 重建
