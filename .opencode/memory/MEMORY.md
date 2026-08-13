# Memory Index

- [项目概况](project-overview.md) — OpenUTAU Plus 增强分支背景与进展
- [用户偏好](user-preferences.md) — 中文沟通、原子化 commit、注重 UI 细节
- [VST Phase 1-2 准备](vst-phase-3-prep.md) — VST C# 侧基础设施就绪，待 C++ 桥接
- [2026-07 审计与重构计划](audit-2026-07-refactor.md) — 全面审计结论 + 分阶段重构方案，报告在 docs/AUDIT.md
- [会话成就 2025-07-16](2025-07-16-session-achievements.md) — 早期会话成果
- [无明文密钥](no-plaintext-secrets.md) — 安全约定
- [UI 重构状态](ui-redesign-phase.md) — 暖灰暗色主题 · PlusTheme 已退役转向 SukiUI · 设计决策已确认
- [SukiUI 替换计划](sukiui-replacement.md) — 阶段 A-E 全部收官，UI 改造已全部结束（2026-08-12 用户确认，待办清空）
- [侧栏素材库 plan](sidebar-library-plan.md) — E4 已确认：歌手列表+伴奏库（双击/拖拽新建轨道、试听、铭牌 Classic/DiffSinger、圆角矩形头像、扫描不读元数据）
- [视觉自检与能力边界](image-recognize-capability.md) — auto-look 截图识别闭环可用 · 不能手操界面（唯一短板）
- [UI 预览库](ui-preview-library.md) — 预览 HTML 位置 · 设计 Token 速查 · 字体/图标路径
- [右键菜单调试](context-menu-debug.md) — 幽灵弹窗/点外不关闭 · 根因线索 OverlayLayer 缺失 · 复现测试待续
- [Avalonia 12 升级](avalonia-12-upgrade.md) — 2026-08-01 完成 · WindowDrawnDecorations 踩坑 · Rx 6.x ObservableExtensions 坑
- [音频管线重构](audio-pipeline-refactor.md) — 2026-08-04 四阶段完成 · B1 竞态/RenderGate/两批播放/ExportSession/VST 命令化 · 踩坑清单
- [VST GUI 卡死重大修复](vst-gui-thread-fix.md) — 2026-08-04 VST 专用线程方案 · 四轮失败方案教训 · MsgWait 漏 SendMessage 坑 · VstTest 桥接测试工具
- [git 推送 SSL](git-push-ssl.md) — 本机推送 GitHub 需 -c http.sslVerify=false
