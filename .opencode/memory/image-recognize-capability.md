---
name: image-recognize-capability
description: 视觉自检闭环（截图+识别）可用，但无法手操界面（唯一短板）
metadata:
  node_type: memory
  type: feedback
  originSessionId: 84d7e7cc-b226-48d1-935d-a3e22b84ee0a
---

# 视觉自检闭环 + 能力边界（2026-08-02 用户确认）

## 用法约定（2026-08-02 用户规定，已写入 CLAUDE.md）
- **一般情况下不使用自截图**；需要截图参考时由**用户主动提供**截图 → 用 recognize.py 识别
- 用户提供的截图路径可能带空格/中文（用引号包裹）；识别结果用于对照修改意图

## 已可用（用户加了两个 skill）
- **image-recognize**（`C:\Users\XKLMY\.claude\skills\image-recognize\`）：
  - `recognize.py <图片路径> [--prompt] [-o]`：识别已有图片（详细描述：布局/颜色/坐标/文字）——**主用**
  - `auto-look.py --window OpenUTAU --list` / `--hwnd <句柄>` / `--check`：窗口枚举/指定截图/截屏+识别一体（用户要求指定窗口截图时用）
  - 模型：智谱 GLM-4V-Flash（免费），截图会上传智谱服务器
- **verify**（项目 .claude/skills/verify/）：构建/启动/日志检查 + 功能验证清单（保存/导出/混音台/效果器/自动保存）

## ⚠️ 能力边界（用户 2026-08-02 明确：这是唯一短板）
- **不能手操界面**：无法模拟点击/键盘输入/拖拽等交互操作（只有截图识别，没有 UI 自动化驱动）
- 交互类验证（弹窗交互流程、菜单展开后状态、拖拽行为等）必须靠**用户实机操作**
- 能做的：截图看静态视觉（布局/颜色/文字/样式），配合探针测试（headless 数值验证）
- 设计验证流程时：静态视觉 → 截图自检；交互行为 → 用户实机确认

**Why:** 用户补充多模态能力后明确剩余短板，避免我误以为可以全自动 UI 验证。
**How to apply:** UI 改动验证分两类——视觉类用 auto-look 自检，交互类列清单请用户实机；相关 [[sukiui-replacement]]。
