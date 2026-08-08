# Search — 文件索引与快速搜索

WinBox 第一阶段能力插件：本地文件索引 + 快速搜索，目标是逐步替代 Listary 的核心工作流。

设计决策摘要见下文；实现落在 `WinBox.Search/Index`（存储 / 扫描 / 增量）与 `WinBox.Search/Query`（匹配 / 排序）。

## 目标体验

- **全局唤起**：快捷键呼出搜索框，输入即搜（Host 唤起层 + 本插件作为默认 `IQueryHandler`）
- **路径 / 文件名检索**：按配置范围建索引，结果按相关度排序
- **一键动作**：打开、打开所在文件夹、复制路径等
- **增量更新**：监听文件系统变更，避免日常全量重建

## 当前实现

| 路径 | 职责 |
|------|------|
| `WinBox.Search/` | 插件入口（`SearchPlugin`） |
| `WinBox.Search/Index/` | `IndexOptions` / `IndexPolicy` / `DirectoryScanner` + 内存文件名索引 |
| `WinBox.Search/Query/` | 子串匹配与简单排序（后续：模糊 / trigram） |

协议定义在 `src/WinBox.Abstractions`（`IWinBoxPlugin` / `ISearchService` / `IQueryHandler`）。

已落地：**按 roots 递归扫盘**（白/黑名单、扩展名过滤、不跟随 reparse point）→ 写入文件名索引 → `RebuildIndexAsync`。  
Host 暂硬编码窄 roots（如 `D:\Github\proposal`），**配置面板下一步**。单测在 `%TEMP%` 下落盘、建索、清理。

---

## 索引设计（已对齐）

### 1. 建索引范围

两层核心维度，再加可选白/黑名单（**能力必做**，配置可为空）：

| 维度 | 作用 |
|------|------|
| **目标路径** | 扫哪些根目录（文档、代码仓、盘符等） |
| **目标文件类型** | 按扩展名收窄收录 |
| **白名单 / 黑名单** | 路径与扩展名均可选；空 = 不额外限制 |

建议把规则分成：

1. **扫描规则（Scanner policy）**：决定是否进入目录 / 是否收录文件  
2. **结果规则（Query filter，可后做）**：索引中有，但默认搜索不展示（如隐藏文件）

冲突时：**黑名单优先于白名单**。

默认就应较强的路径黑名单示例：`node_modules`、`.git`、`__pycache__`、回收站、临时目录、浏览器缓存等。

路径相关约定（可分期落地）：

- 根路径列表 + 是否递归
- 符号链接 / 交接点：MVP **不跟随**
- 网络盘 / 云盘占位文件：MVP 可标不支持或只索引已落盘文件

配置模型（设置 UI 可后补，模型宜先定）：

```text
roots[]
includeExtensions?          # 空 = 不限制类型
excludeExtensions?
includePathPatterns?        # 可选
excludePathPatterns[]       # 建议带合理默认
```

### 2. 建索引方式与查询方式

查询能力取决于已建的索引层。各层独立，不要绑成一张大杂烩表：

| 索引层 | 存什么 | 查询体验 | 成本 | 阶段 |
|--------|--------|----------|------|------|
| **文件名** | path + name + ext（+ 可选 mtime/size） | 子串 / 前缀；后期模糊 | 极低 | **P1 必做** |
| **文件类型** | 作为文件名记录上的 `Extension` 字段 | 按类型过滤 / 后续 `ext:` | 几乎 0 | **并入 P1**（不必第二套索引） |
| **内容全文** | 分词 / 倒排 + 文本提取 | 内容检索 | 很高 | **P2+ 可选开关** |
| **标题 / 摘要** | 从 MD/HTML/PDF/Office 抽 title 或短摘要 | 比全文轻，仍要解析器 | 中高 | **P2 候选**（优先便宜格式） |

**P1 索引记录建议至少包含**：`FullPath`、`FileName`、`Extension`、`LastWriteTimeUtc`。

**P1 查询**：文件名命中优先，路径次之（与当前 `SubstringSearchEngine` 一致）。

业界对照：

- **文件名 / 路径派**（Everything、Listary 等）：元数据索引 + USN/变更日志 → 体积小、可全盘秒搜。**P1 对齐此派。**
- **内容派**（Windows Search、Spotlight、Recoll 等）：提取 + 倒排，磁盘与 CPU 成本高；长博客 / PDF / Word / paper 场景尤甚。

对长文档为主的机器：**默认只做文件名**；全文必须显式开关，并按路径/扩展名二次收窄。标题/摘要维护成本接近「半个全文引擎」，等文件名体验稳定后再做。

粗估：文件名索引约百字节级/文件；全文可到 KB～数十 KB/文档并快速膨胀到 GB。

### 3. 增量更新：如何知道新建 / 删除 / 重命名？

增量解决的是 **索引集合与磁盘一致**，与是否做全文无关。事件 → 索引操作：

| 磁盘事件 | 索引动作 |
|----------|----------|
| 新建 | `Upsert`（先过白/黑名单） |
| 删除 | `Remove` |
| 重命名 / 移动 | `Rename(old→new)` 或删旧加新 |
| 改内容 / 属性 | 文件名索引通常可忽略；有 mtime/size 再更新元数据 |

三种机制：

| 方式 | 原理 | 适用 |
|------|------|------|
| **NTFS USN Journal** | 卷级变更日志；记 `NextUsn`，追新记录；rename 常成对出现 | Everything 同款主路径；整盘 / 大范围 |
| **目录通知**（`ReadDirectoryChangesW` / `FileSystemWatcher`） | 对配置的 roots 回调 Created/Deleted/Renamed | 实现快；小范围；高负载可能丢事件 |
| **定时对账** | 快照 diff 或抽样子树 | 安全网；USN/Watcher 失效时 reconcile |

推荐组合：

```text
冷启动：全量扫（应用白/黑名单）→ 持久化索引 + 记下 USN 游标（或 Watcher）
运行中：USN（主）或 Watcher（过渡）→ Upsert / Remove / Rename
兜底：  Journal 失效 / 丢事件 → 全量重建或定时 reconcile
```

USN 注意：主要面向 NTFS；Journal 被清或 ID 变化时必须能 fallback；P/Invoke 隔离在小 façade 后便于测试。

### 4. 落地阶段

1. **Index policy + 扫盘**：按 roots 递归，应用白/黑名单，写入文件名索引（替换演示路径）
2. **持久化 + 启动加载**
3. **Activate**：`OpenPath` / 打开所在目录 / 复制路径
4. **增量**：先对 roots 用 Watcher；再上 NTFS USN 作主增量
5. **可选内容 / 标题索引**：独立 `ContentIndex`，按格式插件化，默认关闭

---

## 非目标（MVP / P1 不做）

- 内容全文检索作为默认能力（仅后续可选数据源）
- PDF/Word 标题或摘要提取（P2）
- 云盘 / 网络共享的完整同步语义
- 多索引融合排序（filename + content）
- 插件市场与在线更新

## 本地验证

```bash
dotnet test tests/WinBox.Search.Tests
```
