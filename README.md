# WinBox

Windows 工具箱。插件式、可扩展，按需安装能力——而不是又一个大而全的全家桶。

## 为什么做这个

Windows 一直缺乏一些本该与窗口系统深度集成的实用效率工具——窗口管理、快速启动、系统级搜索、快捷键编排等。系统层没补上，生态里就长出各种五花八门的小工具：功能重叠、互不兼容、装一堆才凑齐一套工作流。

搜索只是其中最扎眼的一块：自带索引体验很弱；PowerToys 功能强，但过于臃肿；Listary 好用，定制不够灵活，高级能力还有付费门槛。我们需要的不是「装完就有一切」，而是：

- **插件式**：需要什么装什么，不需要的不占地方
- **可扩展**：用统一接口接入索引、搜索、快捷键、启动页等能力
- **可自定义**：行为、数据源、快捷方式都可以按自己的工作流改

## 定位

WinBox 是一个面向 Windows 的轻量宿主应用：核心只做插件发现、加载与通信，具体能力由插件提供。

第一阶段聚焦 **文件索引与搜索**，打好可扩展底座后再逐步接其他工具类能力。

## 路线图（初稿）

| 阶段 | 目标 |
|------|------|
| 0 | 项目骨架、插件协议、宿主启动 |
| 1 | 本地文件索引 + 快速搜索（MVP） |
| 2 | 搜索插件扩展点（过滤器、预览、动作） |
| 3 | 更多能力插件（快捷键、启动页等） |

## 设计原则

1. **小核心，大插件** — 宿主保持精简，功能下沉到插件
2. **按需安装** — 插件可独立安装 / 启用 / 禁用
3. **接口稳定、实现可换** — 索引引擎、UI、数据源都可以替换
4. **先可用，再完美** — MVP 优先，避免过早做成「另一个 PowerToys」

## 技术栈

- **语言 / 运行时**：C# / .NET 8（LTS）
- **测试**：xUnit
- **CI**：GitHub Actions（`windows-latest`：restore → build → test）
- **UI（后续）**：WinUI 3 / WPF（阶段 1 搜索唤起层再接入）
- **插件边界（后续）**：先进程内加载，再视需要做隔离

你不需要先精通 C#：方向与取舍由人定，实现与测试管线可交给 AI 迭代。

## 仓库结构

```
winbox/
├── WinBox.sln
├── global.json                 # 锁定 SDK 主版本
├── Directory.Build.props       # 统一可空引用 / 警告策略
├── nuget.config
├── .github/workflows/ci.yml
├── src/
│   ├── WinBox.Abstractions/    # 插件与搜索协议
│   └── WinBox.Host/            # 宿主（当前：控制台演示）
├── plugins/
│   └── search/
│       └── WinBox.Search/      # 索引 + 查询 MVP 骨架
└── tests/
    ├── WinBox.Host.Tests/
    └── WinBox.Search.Tests/
```

## 本地开发

```bash
make help      # 查看常用命令
make build     # 编译
make test      # 跑测试
make run       # 启动宿主（Shift+Alt+U 唤起输入框，Esc 关闭）
make ci        # 对齐 CI：Release 编译 + 测试
```

等价的 `dotnet` 命令：

```bash
dotnet restore WinBox.sln
dotnet build WinBox.sln
dotnet test WinBox.sln
dotnet run --project src/WinBox.Host
```

## 状态

🚧 阶段 0：已选定 C#/.NET 8，落地解决方案、最小插件协议、内存搜索骨架与 CI。

## 贡献

欢迎提想法与 Issue。第一次接触本仓库或 C# 也没关系：

- [贡献指南（写给小白）](CONTRIBUTING.md) — 工程结构、运行原理、改哪里、怎么验证
- [Agent Skills 总览](.cursor/skills/README.md) — 用流程捍卫质量；贡献者带热忱与创意即可

插件协议会随阶段 1 继续演进。

## License

待定。
