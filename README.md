# WuwaModModifier

WuwaModModifier 是一个基于 WPF/.NET 8 的 Windows 桌面工具，用于分析、编辑并同步 Wuthering Waves MOD 配置文件（ini）。

## 核心能力

- 配置分析
  - 解析按键切换（Key 节）
  - 解析参数定义与引用
  - 解析模型显示项（Visibility）
- 多层配置文件识别
  - 支持同一 MOD 目录内多层 ini 候选的自动发现与分析
  - 典型结构示例：

```text
mod.ini
1/mod.ini
2/mod.ini
3/mod.ini
4/mod.ini
```

- 候选配置切换
  - 在配置分析区直接切换候选 ini
  - 下拉显示短路径，内部仍使用完整路径，保证保存/同步行为准确
- 跨来源候选对齐
  - 在 Mod 配置 与 WWMI 配置之间切换时，尽量保持同名/同路径后缀的候选项
- 保存与同步
  - 保存到 Mod
  - 保存到 WWMI
  - Mod -> WWMI 同步
  - WWMI -> Mod 同步
- 版本同步窗口
  - 支持候选发现、自动配对、手动配对与批量应用

## 技术栈

- .NET 8
- WPF
- MVVM
- xUnit（单元测试）

## 目录结构

- [WuwaModModifier](WuwaModModifier): 主程序代码（WPF UI、ViewModel、业务逻辑）
- [UnitTests](UnitTests): 单元测试
- [docs](docs): 维护文档

## 快速开始

### 环境要求

- Windows
- .NET SDK 8+

### 构建

```powershell
dotnet build .\WuwaModModifier.sln
```

### 运行

```powershell
dotnet run --project .\WuwaModModifier\WuwaModModifier.csproj
```

### 测试

```powershell
dotnet test .\UnitTests\UnitTests.csproj
```

## 使用流程建议

1. 在主界面选择 MOD 根目录与 WWMI 目录。
2. 在目录树中选择具体 MOD。
3. 在配置分析区查看候选配置并按需切换。
4. 完成编辑后，使用四个按钮执行保存或双向同步。
5. 需要批量更新时，使用版本同步窗口进行配对与应用。

## 文档

- [docs/README.md](docs/README.md)
- [docs/mod-config-analysis.md](docs/mod-config-analysis.md)

## 注意事项

- 如果构建失败并提示 `WuwaModModifier.exe` 被占用，请先关闭正在运行的程序后重试。
- 多层候选会按评分与路径深度排序，通常根层 `mod.ini` 优先，但不会忽略子层候选。

## 许可证

本仓库许可证见 [LICENSE.txt](LICENSE.txt)。
