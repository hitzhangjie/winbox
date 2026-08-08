# 贡献指南（写给第一次接触本仓库 / C# 的朋友）

欢迎来玩 WinBox。这份文档用尽量少的行话，说明：**仓库是怎么组织的、程序跑起来时发生了什么、你改代码时该动哪里**。

不要求你先精通 C#。能装好 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)、会用 `make`（或直接用 `dotnet` 命令）就够上手。

---

## 1. 先跑起来

在仓库根目录：

```bash
make help      # 看常用命令
make build     # 编译
make test      # 跑测试（改代码后建议必跑）
make run       # 启动控制台演示：输入关键字搜索，空行退出
make ci        # 对齐 GitHub CI：Release 编译 + 测试
```

没有 `make` 时，等价命令见根目录 [README.md](README.md) 的「本地开发」一节。

> **说明**：`make run` / `dotnet run` 是启动**宿主演示程序**；`make test` 才是跑自动化测试。两者不是一回事。

---

## 2. 用一张表认清 C# 工程词

| 概念 | 一句话 | 在本仓库里 |
|------|--------|------------|
| **Solution（`.sln`）** | 工程清单，把多个项目捆在一起编译 | `WinBox.sln` |
| **Project（`.csproj`）** | 一个可编译单元，产物一般是 `.dll` | Host / Search / Abstractions / Tests |
| **引用（ProjectReference）** | A 依赖 B → 编译 A 时能用 B 的公开类型 | 见下一节的箭头图 |
| **接口（`interface`）** | 只约定「能做什么」，不写怎么做 | `IWinBoxPlugin`、`ISearchService` |

`make build` 做的事：按依赖顺序把每个项目编成程序集；Host 再作为入口被 `make run` 启动。

---

## 3. 目录 = 职责

```
WinBox.sln
├── src/
│   ├── WinBox.Abstractions/   ← 合同：接口，几乎没有实现
│   └── WinBox.Host/           ← 宿主：启动、注册插件、演示交互（当前是控制台）
├── plugins/search/
│   └── WinBox.Search/         ← 能力：真正的索引 + 搜索
│       ├── Index/             ← 存路径（现在是内存）
│       └── Query/             ← 按关键字匹配、打分
└── tests/                     ← 验证上面几块有没有按合同工作
```

### 谁可以依赖谁（很重要）

```text
WinBox.Host ──────────► WinBox.Abstractions
       │                        ▲
       └──────► WinBox.Search ──┘
```

- **Abstractions 不依赖任何人**：合同要稳定，大家都能引用。
- **Search 只依赖 Abstractions**：插件实现合同，**不要**反过来依赖 Host。
- **Host 依赖两者**：负责组装插件并驱动运行。
- **Tests 引用被测项目**：像外部用户一样调用公开 API。

一句话：**Host 不该知道搜索「怎么实现」，只认接口。** 以后换更好的索引引擎，只要还实现 `ISearchService`，Host 可以少改甚至不改。

---

## 4. 程序跑起来时发生了什么

`make run` 最终执行 Host 入口（`src/WinBox.Host/Program.cs`），顺序可以记成四步：

1. **注册**：`new SearchPlugin()` 放进 `PluginRegistry`
2. **启动**：`StartAllAsync()` → 每个插件的 `StartAsync()`
3. **按能力取用**：`GetRequired<ISearchService>()` —— 要的是「能搜索」，不是某个具体类名
4. **建索引 → 查询循环**：用户输入 → `SearchAsync` → 打印结果 → 退出时 `StopAllAsync`

搜索插件 `SearchPlugin` 同时实现两个接口：

- `IWinBoxPlugin`：对宿主来说是「一个可启停的插件」
- `ISearchService`：对功能来说是「能建索引、能搜索」

内部再交给：

- `Index/InMemoryFileIndex`：存路径
- `Query/SubstringSearchEngine`：子串匹配 + 简单排序

当前索引数据是演示用的几条假路径，**还没有真正扫你的硬盘**——那是后续阶段的工作。

---

## 5. 现在有什么 / 还没有什么

| 已经有的 | 还只是骨架 / 未做 |
|----------|-------------------|
| 多项目解决方案 + 清晰引用关系 | 从磁盘自动发现插件（现在是代码里 `new`） |
| 插件启停合同 | 真实扫盘、增量索引（如 USN） |
| 内存索引 + 子串搜索 | 全局热键、图形搜索框 |
| 单元测试 + Makefile + GitHub Actions CI | 进程隔离、按需安装插件包 |

改代码前先对一下表，避免在「尚未存在的能力」上空转。

---

## 6. 我想改代码，该动哪里？

| 你想做的事 | 优先看 / 改 |
|------------|-------------|
| 改插件合同（启停、搜索 API） | `src/WinBox.Abstractions/`，并同步改实现与测试 |
| 改宿主如何注册、启动插件 | `src/WinBox.Host/` |
| 改索引存储或扫描逻辑 | `plugins/search/WinBox.Search/Index/` |
| 改匹配、排序、查询行为 | `plugins/search/WinBox.Search/Query/` |
| 改搜索插件对外行为 | `plugins/search/WinBox.Search/SearchPlugin.cs` |
| 加/改自动化验证 | `tests/WinBox.*.Tests/` |

### 小习惯（强烈建议）

1. 改完跑 `make test`（或 `make ci`）。
2. 新增公开行为时，尽量补一条测试，说明「期望是什么」。
3. 保持依赖方向：插件项目不要引用 Host。
4. 大改协议前，先开 Issue 对齐，避免和路线图拧巴。

---

## 7. 质量流程与 AI Skills（重要）

我们**不强求**贡献者是 C# 大佬，但**一定要按流程做事**。

- **你贡献的**：热忱与创意（要解决什么问题、体验怎样更好）
- **流程捍卫的**：高质量维护、测试、交付、分发
- **AI Skills 保驾的**：零成本启动、改哪里、怎么测、怎么达到可合并/可发版

如果你用 Cursor（或同类 Agent）参与贡献，仓库内置了操作手册：

| Skill | 作用 |
|-------|------|
| `.cursor/skills/winbox-onboarding` | 第一次进仓、环境与定向 |
| `.cursor/skills/winbox-change-loop` | **任何改动的默认闭环**（必走） |
| `.cursor/skills/winbox-architecture` | 目录边界与依赖法则 |
| `.cursor/skills/winbox-testing` | 测试与 CI 门槛 |
| `.cursor/skills/winbox-ship` | PR / 评审 / 发版分发 |

总览见 [.cursor/skills/README.md](.cursor/skills/README.md)。Agent 侧还有始终生效的规则：`.cursor/rules/winbox-quality.mdc`。

对人类贡献者：你仍以本文 + `make test` 为准；把想法说清楚即可，工程落地可交给遵循上述 skills 的 Agent。

---

## 8. 提交与讨论

- **Issue**：想法、缺陷、设计讨论都可以。不确定该不该做时，先开 Issue。
- **Pull Request**：说明「为什么改」比罗列文件名更重要；附上你本地 `make test` 的结果更佳。
- **代码风格**：仓库用 `Directory.Build.props` 统一了可空引用等设置；以能通过 `make build` / CI 为准。

当前阶段聚焦 **文件索引与快速搜索（替代 Listary 的核心工作流）**。窗口管理、快捷键编排等是更后面的插件方向，欢迎讨论，但默认优先把搜索链路做扎实。

---

## 9. 只记一句

**.sln 管有哪些项目，.csproj 管谁引用谁，interface 管协作边界，Program.cs 是把零件拧在一起开机的那一步。流程 + 测试管能不能进主干。**

有问题直接开 Issue。第一次贡献不必追求完美，能跑通测试的小改动就非常欢迎。
