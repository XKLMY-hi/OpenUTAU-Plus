---
name: utvtu-migration
description: 2026-09-18 项目迁移 — 新仓库 UTvTU（全历史）、旧 OUP 仓库撤包归档、远程改名约定
metadata:
  node_type: memory
  type: project
---

# UTvTU 迁移（2026-09-18）

## 已完成的动作

| 项 | 结果 |
|---|---|
| 新仓库 | **https://github.com/XKLMY-hi/UTvTU**（public，默认分支 `plus-develop`），已推 `plus-develop` + `master`，**保留全部提交历史**（`.git` 128 MB，历史可直接推） |
| 旧仓库 | `XKLMY-hi/OpenUTAU-Plus` → 2 个 Plus release **已删除**、3 个 Plus 标签**已删除**；该仓库只剩上游带来的历史 release（fork 时继承），按用户决定**保留** |
| 本地远程 | `origin` = **UTvTU**（新）；`old-origin` = OpenUTAU-Plus（旧，归档）；`upstream` = openutau/OpenUtau（不变） |
| 部署产物 | `packaging/dist/**`（178 MB 安装包）与 `packaging/publish/**`（约 1 GB 发布产物）已删除（**均未跟踪**，git 历史不受影响） |

## 关键坑

- **`gh` 默认认 upstream 仓库**：不带 `--repo` 时 `gh release list` 读的是 `openutau/OpenUtau`，会看到 33 个上游 release（全是假的"我们的 release"）。**查本仓库必须显式 `--repo XKLMY-hi/<repo>`**。
- 旧仓库的 release 表面上有 2026-09 的 `0.1.570.x-alpha`，那也是 fork 时从上游继承的，不是我们发的。

## 待决/未做（用户明确"先不急"）

1. **更名范围**（用户强调重要、暂缓）：产品显示名/程序集名/命名空间/文件扩展名要改到哪一层。当前代码与文案**仍是 OpenUTAU Plus**，暂存扫描结果：
   - 品牌文本 **157 处 / 53 文件**（`OpenUTAU Plus` / `OpenUtau Plus` / `UTAU Plus`）
   - 仓库 URL **19 处 / 12 文件**（README、更新检查、CI、issue 模板）
   - `PlusInfo.cs`（7 处引用）、`OpenUtau/Themes/Plus.Resources.axaml`、`packaging/OpenUtauPlus.iss`
   - ⚠️ `runtimes/vst3sdk/**/plus.svg` 是 VST3 SDK 自带文件，**不可改名**
2. **舍弃部署器**（待执行）：删除跟踪中的 `packaging/{OpenUtauPlus.iss, build-installer.ps1, ChineseSimplified.isl, release-notes-*.md}`，以及上游带来的 `.github/workflows/{build.yml, release-cleanup.yml, stale.yml, pr-test.yml, worldline-build.yml}`；`packaging/vc_redist.x64.exe`（未跟踪）一并删
3. **更新检查 URL 漂移风险（用户可感知）**：程序内 updater 仍查 `XKLMY-hi/OpenUTAU-Plus`，而该仓库已无 Plus release → 装 0.0.3-beta 的用户会被提示"有更新"并降级。要么改 URL、要么暂时关闭更新检查
4. 旧仓库 README 加**迁移标识**（计划用"归档版 README + 在 GitHub 上 archive 仓库"两步，旧 tag 缺失导致的旧版附件链接会失效，需在公告里说明）
