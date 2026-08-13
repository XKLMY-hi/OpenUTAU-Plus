---
name: user-preferences
description: 用户偏好与工作风格
metadata: 
  node_type: memory
  type: user
  originSessionId: e0a03d4d-5f8d-4646-8e07-4b2464f13baa
---

用户是 OpenUTAU Plus 分支的唯一开发者，中文为主要沟通语言。

## 工作风格
- 提交信息使用中文描述
- 注重 UI/UX 细节打磨
- 偏好原子化、描述性的 commit
- 分支策略：功能在 `plus-develop` 上直接开发
- 硬性要求：每步代码改动后必须用 verify skill 做端到端运行时验证，不能只靠 dotnet build/dotnet test
- **阶段闸门**：每个实施阶段完成后必须先让用户实机预览确认（必要时截图），确认后才进下一阶段；预览发现的问题本阶段内修完
- **沟通方式**：偏好直接讨论和明确推荐，多次拒绝"多选题/选项罗列"——给出一个明确建议比列一堆选项更有效；认可推荐方案会直接说"可以"

**Why:** 新会话需了解用户沟通和编码风格。
**How to apply:** 使用中文回复，commit 信息用中文，注重代码细节；给明确推荐而非罗列选项；大改动分阶段并每阶段让用户预览。
