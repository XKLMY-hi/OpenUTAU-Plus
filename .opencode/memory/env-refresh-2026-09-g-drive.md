---
name: env-refresh-2026-09-g-drive
description: 2026-09 环境重启后构建链重建 — G 盘工作区、E 盘消失、SDK 10/9、离线 NuGet、obj 残留只读处置
metadata:
  node_type: memory
  type: project
---

# 环境重启后的构建链重建（2026-09 本次会话实测）

上次会话（2026-09-16）的构建环境已**整体失效**——那次记录的 `E:\tools\dotnet`（便携 SDK 9）、`E:\home\.nuget\packages`（离线包缓存）在本机已不存在。本次重新探明并跑通：

## 事实

| 项 | 上次会话（已失效） | 现在 |
|---|---|---|
| 工作区 | 同（G 盘，盘符重分配后一致） | `G:\xklmy文件夹\vibe coding\OpenUTAU Plus` |
| dotnet | `E:\tools\dotnet`（便携 9） | `C:\Program Files\dotnet`（系统）：SDK 8.0.400 / 9.0.317 / 10.0.400，运行时 6/8.0.8/9.0.17/9.0.19/10.0.11 |
| NuGet 缓存 | `E:\home\.nuget\packages` | `C:\Users\XKLMY\.nuget\packages`（313 包，已含 Avalonia 12.1.0、System.Text.Json 9.0.2 等） |
| 网络 | 无 | 仍无（nuget.org SSL 不通），只能离线还原 |
| DOTNET_ROOT | 需手工清空才能跑 app | **无需任何处理**（shell 里根本没有该变量） |

**SDK 版本结论**：当前默认 SDK 10.0.400 直接编译本 net8.0 解决方案**成功**（1578 警告 0 错误，实测 74 秒），不再需要上次会话的 `DOTNET_ROOT=E:\tools\dotnet` 手法，也**没有**加 global.json。若将来 SDK 10 出现分析器/还原兼容问题，退路是 `DOTNET_ROOT="C:\Program Files\dotnet" dotnet ... -p:...` 指定 9.0.317（本机已装）。

## 命令（当前有效，实测 exit 0）

```powershell
# 1) 离线还原（sln 顺序即可，VstProbe 若报 NETSDK1127 再单还原一次 -p:RuntimeIdentifiers=）
dotnet restore OpenUtau.sln -m:1 -p:TreatWarningsAsErrors=false --ignore-failed-sources
dotnet restore VstProbe\VstProbe.csproj -m:1 -p:RuntimeIdentifiers= -p:TreatWarningsAsErrors=false --ignore-failed-sources

# 2) 构建
dotnet build OpenUtau.sln --no-restore -m:1 -p:RuntimeIdentifiers= -p:UsedAvaloniaProducts=

# 3) 测试（基线 284/284）
dotnet test OpenUtau.Test\OpenUtau.Test.csproj --no-build

# 4) 运行
.\OpenUtau\bin\Debug\net8.0-windows\OpenUtau.exe
```

## 坑：上次会话遗留的 obj 全是"只读"

`obj/**` 里 E 盘时代生成的中间产物（`*.nuget.g.props`、`project.assets.json`、`AssemblyReference.cache`、`Avalonia/references`）在本环境**无法覆盖也无法删除**（`Access to the path ... is denied`，ACL 正常、非只读属性）→ restore 直接失败。

**处置**：`Get-ChildItem -Recurse -Directory -Filter obj | Remove-Item -Recurse -Force` 全部删掉重来（本次已删除并重建，构建恢复）。同理，若 `bin` 或任何历史产物报同类错误，删掉重建即可——这些目录都在 `.gitignore` 内，无损失。

**How to apply:** 恢复会话时不必再找 E 盘或设 DOTNET_ROOT；直接按上面 4 条命令走。遇到 `Access to the path` 且 ACL 正常 → 判为历史只读产物，删除该目录重建。
