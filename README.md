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

当前主线：**本地文件索引 + 快速搜索 MVP**，并已接上托盘 / 唤起层 / 设置与基础工具箱能力。

## 现在能做什么（重点）

阶段 0 骨架与阶段 1 MVP 已可日常试用：

| 能力 | 说明 |
|------|------|
| 全局热键唤起 | `Shift+Alt+U` 打开 launcher；`Esc` 关闭 |
| 文件名快速搜索 | 按配置 roots 扫盘建内存索引；子串匹配 + 简单排序 |
| Listary 风格 File Search | 类型 / 修改时间过滤、展开查看、资源管理器风格图标 |
| 索引设置 | 托盘 → Index settings；配置写入 `%LocalAppData%\WinBox\` |
| 工具箱插件 | 计算器、CMD、可配置 Web 前缀搜索（Settings → Web）、AI 骨架 |
| 开机自启 | Settings → General → Start with Windows |
| 质量门禁 | `make test` / `make ci` + GitHub Actions CI |
| 分发打包 | `make dist` → 便携 zip + Setup 安装包；打 tag / 发 Release 自动挂附件 |

尚未完成（有意延后）：持久化索引、USN/Watcher 增量、OpenPath 等激活动作、插件按需发现安装、代码签名 / winget。

## 支持平台

| 项 | 当前支持 |
|----|----------|
| 操作系统 | **Windows 11** |
| 处理器 | **amd64（x64）** RID：`win-x64` |
| 安装形态 | **便携 zip**（解压即用）+ **Setup.exe**（Inno Setup，装到 Program Files） |

CI 在 `windows-latest` 上构建上述产物。工作流矩阵已预留扩展位；其它架构 / 系统版本暂不承诺。

## 路线图（初稿）

| 阶段 | 目标 | 状态 |
|------|------|------|
| 0 | 项目骨架、插件协议、宿主启动 | ✅ |
| 1 | 本地文件索引 + 快速搜索（MVP）+ 唤起层 | ✅ 可用（增量/持久化继续） |
| 2 | 搜索插件扩展点（过滤器、预览、动作） | 进行中 / 部分落地 |
| 3 | 更多能力插件（快捷键、启动页等） | 未开始 |

## 设计原则

1. **小核心，大插件** — 宿主保持精简，功能下沉到插件
2. **按需安装** — 插件可独立安装 / 启用 / 禁用
3. **接口稳定、实现可换** — 索引引擎、UI、数据源都可以替换
4. **先可用，再完美** — MVP 优先，避免过早做成「另一个 PowerToys」

## 技术栈

- **语言 / 运行时**：C# / .NET 8（LTS），宿主为 WPF + WinForms 托盘
- **测试**：xUnit
- **CI**：GitHub Actions — `CI`（push/PR：restore → build → test）；`Dist`（tag / Release：打包并挂附件）
- **插件边界**：当前进程内显式注册；后续再考虑发现与隔离

你不需要先精通 C#：方向与取舍由人定，实现与测试管线可交给 AI 迭代。

## 仓库结构

```
winbox/
├── WinBox.sln
├── global.json                 # 锁定 SDK 主版本
├── Directory.Build.props       # 统一可空引用 / 版本 / 警告策略
├── Makefile                    # build / test / ci / dist
├── scripts/dist.ps1            # publish → zip + Inno Setup
├── packaging/winbox.iss        # Windows 安装向导脚本
├── .github/workflows/
│   ├── ci.yml                  # PR / 主干质量门禁
│   └── dist.yml                # tag 验证打包；Release 上传附件
├── src/
│   ├── WinBox.Abstractions/    # 插件、搜索、IQueryHandler 协议
│   └── WinBox.Host/            # 宿主：热键、唤起层、QueryRouter、托盘与设置
├── plugins/
│   ├── search/WinBox.Search/   # 文件索引 + 搜索（设计见 plugins/search/README.md）
│   └── toolbox/WinBox.Toolbox/ # 计算器 / CMD / Web / AI 骨架
└── tests/
    ├── WinBox.Host.Tests/
    ├── WinBox.Search.Tests/
    └── WinBox.Toolbox.Tests/
```

## 本地开发

```bash
make help      # 查看常用命令
make build     # 编译
make test      # 跑测试
make run       # 启动宿主（托盘菜单开设置；Shift+Alt+U 唤起；Esc 关闭）
make ci        # 对齐 CI：Release 编译 + 测试
make dist      # 便携 zip + Setup.exe → artifacts/dist/
```

可选指定版本：

```bash
make dist VERSION=0.1.0
```

产物：

| 文件 | 用途 |
|------|------|
| `WinBox-<ver>-win-x64.zip` | 便携包：解压后运行 `WinBox.Host.exe`，无需安装 |
| `WinBox-<ver>-win-x64-setup.exe` | 安装包：向导安装到 Program Files，可卸载、可建快捷方式 |
等价的 `dotnet` 命令：

```bash
dotnet restore WinBox.sln
dotnet build WinBox.sln
dotnet test WinBox.sln
dotnet run --project src/WinBox.Host
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dist.ps1
```

开发态默认 `UseAppHost=false`（避免本机策略误拦原生 exe）；`make dist` 会强制生成可双击的 `WinBox.Host.exe`。

Host 子系统按配置切换，**不用改 csproj**：`make run`（Debug）是带控制台的 `Exe`，方便看日志 / Ctrl+C；`make ci` / `make dist`（Release）是静默托盘的 `WinExe`。

## 发版与安装包

1. **打 tag**（如 `v0.1.0`）→ `Dist` 工作流跑测试并打包，产物上传为 workflow artifact（先验证打包是否健康）。
2. **在 GitHub 创建 Release**（基于该 tag）→ 同一工作流再次打包，并把 **zip + Setup.exe** 挂到 Release 附件。
3. 用户按习惯二选一：
   - 便携：下载 zip → 解压 → 运行 `WinBox.Host.exe`
   - 安装：下载 `*-setup.exe` → 下一步安装到 Program Files → 开始菜单启动

版本来源：tag 名（去掉前导 `v`）优先；本地未指定时用 `Directory.Build.props` 中的 `Version`（当前 `0.1.0`）。

本地第一次打安装包时，若未安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)，`scripts/dist.ps1` 会尝试自动下载并装到 `%LocalAppData%\Programs\Inno Setup 6`。CI 则通过 Chocolatey 安装。
## 贡献

欢迎提想法与 Issue。第一次接触本仓库或 C# 也没关系：

- [贡献指南（写给小白）](CONTRIBUTING.md) — 工程结构、运行原理、改哪里、怎么验证
- [Agent Skills 总览](.cursor/skills/README.md) — 用流程捍卫质量；贡献者带热忱与创意即可

## License

待定。
