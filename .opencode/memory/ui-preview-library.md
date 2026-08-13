---
name: ui-preview-library
description: UI 预览库位置和结构 — 暖灰暗色主题的设计参考实现
metadata: 
  node_type: memory
  type: reference
  originSessionId: 1356e18c-ddc4-4688-962b-eb3824a728f3
---

# UI 预览库

**位置**: `D:\xklmy文件夹\XK\XKLMY\项目\vibe coding\UI\`

## 文件

- `index.html` — 完整设计系统预览（Design Tokens → 所有控件 → 复合组件 → 完整页面）
- `icon-compare.html` — Lucide vs Phosphor vs Heroicons 三列对比 + 混用策略

## 字体

`D:\xklmy文件夹\XK\XKLMY\项目\vibe coding\UI\HarmonyOS Sans\HarmonyOS_Sans_SC\`
- `HarmonyOS_Sans_SC_Regular.ttf` (400)
- `HarmonyOS_Sans_SC_Bold.ttf` (700)
- `HarmonyOS_Sans_SC_Medium.ttf` (500)
- `HarmonyOS_Sans_SC_Light.ttf` (300)

## 设计 Token 速查

| Token | 值 | 用途 |
|-------|-----|------|
| `--bg-base` | `#1e1e28` | 窗口底色 |
| `--bg-surface` | `#282838` | 控件/卡片面 |
| `--bg-hover` | `#303048` | hover 态 |
| `--accent` | `#c73a3f` | Primary red |
| `--accent-hover` | `#d94a50` | Hover red |
| `--border-default` | `#383848` | 控件边框 |
| `--border-subtle` | `#2a2a3a` | 卡片内分隔 |
| `--text-primary` | `#f0f0f0` | 主文字 |
| `--text-secondary` | `#a0a0b0` | 辅助文字 |
| `--r-md` | `8px` | 控件圆角 |
| `--r-lg` | `12px` | 弹窗圆角 |
| `--ctrl-height` | `32px` | 控件高度 |

**Why:** 这是所有 UI 实现工作的视觉参考。每个 Phase 实现前先对照此预览。
**How to apply:** 实现时打开 `file:///D:/xklmy文件夹/XK/XKLMY/项目/vibe coding/UI/index.html` 对照。
