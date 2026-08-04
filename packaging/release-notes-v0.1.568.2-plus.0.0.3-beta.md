# OpenUTAU Plus 0.1.568.2-plus.0.0.3-beta

> **重要**：本项目是 OpenUTAU 的增强分支，**不是原版 OpenUTAU**。原版请前往 [openutau/OpenUtau](https://github.com/openutau/OpenUtau) 获取。

基于 **OpenUTAU v0.1.568.2** 构建。

## 本版本内容

### 新功能
- **Windows 安装器**（Inno Setup）：深色向导、中英双语、与原版完全共存安装
- **钢琴窗实时频谱条**：96 条对数频段随最终输出音频跳动（仅播放时显示）
- **钢琴键试音改 Windows 自带钢琴音源**（MIDI 合成器 GM 钢琴，无 MIDI 设备自动回退正弦波）
- **偏好设置署名区**：README / GitHub 按钮 + 分支作者信息
- **打包字体**：HarmonyOS Sans SC 内嵌（系统未安装也完整呈现）

### 关键修复
- **VST 大插件 GUI 卡死**：controller/attached/消息循环同线程（专用 VST 线程）
- **发布版 VST3 扫描全灭**：vst_probe 独立子目录 + self-contained
- **轨道头设置按钮**：齿轮图标 + Avalonia 12 宿主窗口查找修复
- **原版共存**：单实例按路径匹配（先启动原版再打开 Plus 可同时运行）

### 技术
- 使用 vibe coding（deepseek-v4）开发

## 重要说明（请务必阅读）

- **不要和原版 OpenUTAU 安装在同一文件夹**。安装时可以选择其他位置，但请勿与原版安装到一起。
- **不要将本项目的任何问题提交到原版 OpenUTAU 主线**。本项目问题与原版无关，请在本仓库提交 Issue。
- **本版本为 Beta，未进行深度使用测试**，请尽量不要作为主力工具使用。
- **VST 功能未进行深度测试**：仅测试部分效果器 VST 可用（如 OTT、Persistent Q、TDR Nova）；**音源（乐器）VST 不可用**。
- **架构更换（Avalonia 12 + SukiUI）**，部分界面未充分测试。
- **素材库功能不完整**；**混音台功能不完整**。
- **卸载**：默认保留用户数据（`文档\OpenUtau Plus` 中的声库、工程、配置）。**尽量不要使用第三方卸载器**（Geek 等会按名称启发式扫描，可能误判原版 OpenUTAU 的用户目录为残留）。
- **歌手（声库）文件夹**：默认与原版 OpenUTAU 共用（`文档\OpenUtau\Singers`，存在时自动识别）。若未安装原版且无法识别声库，请在偏好设置中手动定位声库文件夹。
- **与原版共存**：可以和原版同时运行，但需要**先启动原版、再打开 Plus**（原版按进程名判断单实例，先启动 Plus 会被原版误判）。
- **OpenUTAU Plus 或许将始终作为独立分支发展**。

## 安装包

- `OpenUTAU-Plus-win-x64-0.1.568.2-setup.exe`（约 178 MB，自包含，无需安装 .NET）
- 安装时自动检测并静默安装 VC++ 运行库（缺失时）
- 卸载时**保留** `文档\OpenUtau Plus` 用户数据

---

*OpenUTAU Plus — 分支作者：XKLMY。本项目使用 vibe coding（deepseek-v4）。*
