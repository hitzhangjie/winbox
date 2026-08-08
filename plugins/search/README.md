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
| `WinBox.Search/Index/` | `IndexOptions` / `IndexPolicy` / `DirectoryScanner` + 内存 LRU + `SqliteFileIndexStore` + Watcher / USN |
| `WinBox.Search/Query/` | 子串匹配与简单排序（后续：模糊 / trigram） |

协议定义在 `src/WinBox.Abstractions`（`IWinBoxPlugin` / `ISearchService` / `IQueryHandler`）。

已落地：扫盘 → SQLite 持久化 → 启动 `EnsureIndexReadyAsync`；内存为有上限的 LRU 热点缓存（增量**双写**）；运行中 Watcher；USN 冷启动追赶。  
配置：`index-options.json` + 可配 store 目录与 **Max memory for index cache (MB)**。细节见下方 **§3**。

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

配置模型（设置 UI 已对齐）：

```text
roots[]                     # 要扫的根目录
excludeRoots[]              # 排除的目录前缀（其下全部跳过）
includeExtensions?          # 空 = 不限制类型
excludeExtensions?          # 永远跳过；优先于 include
includePathPatterns?        # 可选（高级，面板暂未编辑）
excludePathPatterns[]       # 路径段名黑名单（node_modules 等）
recursive
indexStoreDirectory?        # SQLite 目录；默认 %LocalAppData%\WinBox\index
maxInMemoryMegabytes        # 内存 LRU 预算；默认 512；0 = 不限制
```

扫描判定顺序：excludeRoots → excludePathPatterns → excludeExtensions → includeExtensions → includePathPatterns。

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

### 3. 索引如何持久化、增量更新、支持查询（已实现）

文件名索引的完整语料在 **SQLite**；**内存**是有容量上限的 LRU 热点缓存。二者由 `SearchPlugin` 编排，不是两套可互换的 `IFileIndex` 实现。

#### 3.1 两层分别干什么

| 层 | 实现 | 存什么 | 角色 |
|----|------|--------|------|
| 持久化 | `SqliteFileIndexStore`（`files.db`） | 全部 `FileIndexEntry` + `meta`（fingerprint、USN 游标等） | **权威数据**；重启可恢复；大索引可到 GB 级 |
| 内存缓存 | `InMemoryFileIndex`（Dictionary + LRU） | 最多 `MaxInMemoryMegabytes` 能装下的条目 | **加速**；超容量淘汰最久未用 |

默认库目录：`%LocalAppData%\WinBox\index\`（文件名固定 `files.db`）。  
扫描范围配置仍在 `index-options.json`。  
内存预算：**Settings → Index → Max memory for index cache (MB)**（默认 512；`0` = 不限制）。

```text
  Watcher / USN / Rebuild ──双写──►  Memory (LRU, 有上限)
                                  │
                                  └──►  SQLite (完整语料)

  查询：全量在预算内 → 只读 Memory
        超出预算     → 读 SQLite（完整结果）→ 命中回填 Memory
        单条/FRN miss → SQLite → promote 进 Memory（可踢 LRU）
```

#### 3.2 持久化（怎么落盘、怎么启动）

- **全量重建** `RebuildIndexAsync`：`DirectoryScanner` 扫 roots → `ReplaceAll` 写入 SQLite → 按预算 `LoadAll` 或 `LoadHottest(capacity)` 填内存。
- **启动** `EnsureIndexReadyAsync`（Host 用这条，不是无条件扫盘）：
  1. 打开 SQLite；`options_fingerprint` 与当前 roots/policy 一致则复用库。
  2. 不一致 / 损坏 / 无库 → 全量扫盘重建。
  3. 按内存预算决定全量进 RAM 还是热点预热。
  4. 尝试 USN 追赶进程关闭期间的变更 → 挂 `FileSystemWatcher`。
- fingerprint **不含** store 目录与内存预算（改预算不必重扫盘）。

#### 3.3 增量更新（怎么保持与磁盘一致）

增量目标：索引集合跟上文件系统，与是否全文无关。

| 磁盘事件 | 索引动作 |
|----------|----------|
| 新建 / 内容或属性变更 | `Upsert`（先过 `IndexPolicy`） |
| 删除 | `Remove` |
| 重命名 / 移动 | `Remove(old)` + `Upsert(new)` |
| Watcher 丢事件 / USN Journal 失效 | dirty → **立即**全量 `RebuildIndexAsync`（不是定时任务） |

**双写规则（增量也一样）**：

1. **一定写 SQLite**（权威、可恢复）。
2. **同时写内存缓存**；超过容量时 **LRU 淘汰** 最久未访问的条目。
3. 删除：两边都删；内存里没有该 key 则只影响 SQLite。

**运行时主路径 — FileSystemWatcher**（`IndexChangeWatcher`）：

- 每个配置 root 一个 watcher（可递归）。
- Created / Changed / Deleted / Renamed → debounce 合批 → policy 过滤 → 双写。
- `Error` / buffer overflow → 全量重建兜底。

**冷启动补洞 — NTFS USN**（`Index/Usn/`）：

- 重建后把 `usn_journal_id` / `next_usn` 记在 SQLite `meta`。
- 下次启动：journal 仍有效则从游标追日志；已知 FRN 则双写；未知新建或 journal 丢失 → 全量重建。
- 无权限 / 非 NTFS：跳过 USN，日常仍靠 Watcher。

#### 3.4 查询（怎么搜）

入口：`SearchPlugin.SearchAsync` → `FilteredSearchEngine`（子串排序 + 类型/mtime 等过滤）。

| 情况 | 查询读哪里 |
|------|------------|
| 全部条目都能装进内存预算（`IsFullyMemoryResident`） | **只读内存** `SnapshotEntries` |
| 索引大于预算（内存只是热点子集） | **读 SQLite** `QueryCandidates`，再在进程内排序；命中条目 **promote** 进 LRU |
| FRN / 单路径解析未命中内存 | 查 SQLite，命中则 promote |

因此：磁盘索引可以很大；查询在超预算时仍完整（走 SQLite），内存只加速热点与全量较小时的路径。

#### 3.5 关键类型速查

| 类型 | 路径 |
|------|------|
| 编排 | `SearchPlugin` |
| 内存 LRU | `Index/InMemoryFileIndex.cs` |
| SQLite | `Index/SqliteFileIndexStore.cs` |
| 预算 | `Index/IndexMemoryBudget.cs` |
| Watcher | `Index/IndexChangeWatcher.cs` |
| USN | `Index/Usn/` |
| 扫盘 / 策略 | `DirectoryScanner` / `IndexPolicy` |

#### 3.6 云同步文件夹（iCloud Drive / OneDrive 等）

这类产品通常在 **本机 NTFS 上建一个同步根目录**，客户端把云端变更落成普通本地建/删/改。把该文件夹配进 `roots` 后：

- **可以走同一套增量**：Watcher 盯同步根；同卷 USN 也能看到客户端写入。
- **不必**为 iCloud 单独做云 API；**当前也没有**后台定时扫盘。
- 另一台设备改文件 → 同步到本机目录 → 本地产生文件系统事件 → 现有双写即可跟上（范围是 **已落盘到该文件夹的文件**）。

注意：

| 点 | 影响 |
|----|------|
| 占位 / 仅云端文件（reparse、Files On-Demand 一类） | 扫盘 **不跟随 reparse point**，可能扫不到或只看到壳文件 |
| 大批量同步 | Watcher 易 overflow → dirty → **全量重建** 兜底（已有） |
| 只要「这台机器上已出现的文件名」 | 增量方案够用 |
| 要与云端目录完全一致（含未下载项） | 单靠 Watcher/USN **不够**，需云 API 或定期对账（**均未实现**） |

**定期扫描 / 定时对账：当前未实现。**  
现有兜底只有 Watcher overflow、USN Journal 失效等触发的 **一次性全量 `RebuildIndexAsync`**，以及用户在设置里手动 Save & rebuild。文档里说的「低频 reconcile」只是可选后续方向，**不要理解成产品已在跑定时扫盘**。

### 4. 落地阶段

1. **Index policy + 扫盘**：按 roots 递归，应用白/黑名单，写入文件名索引 — 已做
2. **持久化 + 启动加载** — 已做（SQLite + 可配 store 目录 + 内存预算）
3. **Activate**：`OpenPath`（Enter）/ 打开所在目录（Alt+Enter）— 已接；复制路径后续
4. **增量**：Watcher 双写 + USN 冷启动追赶 — 已做（失败回退全量重建）
5. **可选内容 / 标题索引**：独立 `ContentIndex`，按格式插件化，默认关闭

---

## 非目标（MVP / P1 不做）

- 内容全文检索作为默认能力（仅后续可选数据源）
- PDF/Word 标题或摘要提取（P2）
- 云盘 / 网络共享的**完整**同步语义（占位文件、未下载对象、云侧 API）；本地同步根目录的增量见 [§3.6](#36-云同步文件夹icloud-drive--onedrive-等)
- **定期 / 定时对账扫盘**（未做；丢事件时仅即时全量重建或手动重建）
- 多索引融合排序（filename + content）
- 插件市场与在线更新

## 本地验证

```bash
dotnet test tests/WinBox.Search.Tests
```
