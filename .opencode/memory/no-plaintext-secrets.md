---
name: no-plaintext-secrets
description: 不要问用户要明文 token/密码，用 OAuth 流或让用户在网页上自己操作
metadata: 
  node_type: memory
  type: feedback
  originSessionId: b5fb2a48-571b-4140-8518-8f92e74a5d68
---

# 安全规则

永远不要要求用户提供明文 token、密码、API 密钥等凭证。

**正确做法**：
- GitHub Release：推 tag 后，用户在 github.com/releases 网页创建
- 推送代码：让用户在终端执行 `gh auth login` 走 OAuth 设备流
- 任何 API 调用：优先用 OAuth、设备码登录、或让用户在网页端手动操作

**Why:** 明文 token 在对话中传输存在安全风险，且永远不要养成索要 token 的习惯。
**How to apply:** 遇到需要认证的操作时，永远走 OAuth 设备流或让用户自己在网页端操作。
