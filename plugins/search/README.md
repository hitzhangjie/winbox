# Search — 文件索引与快速搜索

WinBox 第一阶段能力插件：本地文件索引 + 快速搜索，目标是逐步替代 Listary 的核心工作流。

## 目标体验

- **全局唤起**：快捷键呼出搜索框，输入即搜
- **路径 / 文件名检索**：对常见盘符与用户目录建立索引，结果按相关度排序
- **一键动作**：打开、打开所在文件夹、复制路径等
- **增量更新**：监听文件系统变更，避免全量重建拖慢日常使用

## 当前实现（骨架）

| 路径 | 职责 |
|------|------|
| `WinBox.Search/` | 插件入口（`SearchPlugin`） |
| `WinBox.Search/Index/` | 内存索引（后续：持久化 + USN 增量） |
| `WinBox.Search/Query/` | 子串匹配与简单排序（后续：模糊 / trigram） |

协议定义在 `src/WinBox.Abstractions`（`IWinBoxPlugin` / `ISearchService`）。

## 非目标（MVP 不做）

- 内容全文检索（可后续作为可选数据源）
- 云盘 / 网络共享的完整同步语义
- 插件市场与在线更新

## 本地验证

```bash
dotnet test tests/WinBox.Search.Tests
```
