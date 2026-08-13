---
name: git-push-ssl
description: 本机 git 推送 GitHub 需 -c http.sslVerify=false（证书链缺失）
metadata: 
  node_type: memory
  type: project
  originSessionId: 84d7e7cc-b226-48d1-935d-a3e22b84ee0a
---

# git 推送环境（2026-08-04 确认）

本机 git 对 `https://github.com/XKLMY-hi/OpenUTAU-Plus.git` 推送报
`SSL certificate OpenSSL verify result: unable to get local issuer certificate`。

**How to apply:** 推送时用
`git -c http.sslVerify=false push origin plus-develop`
（SSL 校验仅本次跳过，不写入全局配置）。
