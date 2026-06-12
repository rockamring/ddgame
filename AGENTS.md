# AGENTS.md

本文件给后续参与本仓库的编码代理使用。项目目标是从零开始逐步搭建一个可接入 Unity 的游戏客户端框架，并沉淀核心模块、周边工具链和可持续扩展的工程约定。

## 项目定位

- 这是一个 Unity 游戏框架项目的早期骨架，不是一次性完成的完整游戏。
- 当前重点是客户端框架层、游戏逻辑层、公共配置/协议工具链，以及后续 Unity 工程接入的基础。
- 一切设计以易维护、易扩展为第一目标；不要为了“框架化”而牺牲实现清晰度和后续演进空间。
- 代码应优先服务“可扩展、可生成、可验证”，避免为了短期演示写死业务流程。
- 后续新增功能时，应保持模块边界清晰：框架层提供通用能力，逻辑层组合业务规则，工具链负责生成重复性代码和运行时数据。

## 当前目录职责

```text
client/
  GameFramework/      核心框架库，目标框架 netstandard2.1，面向 Unity 接入
    Core/             事件、服务定位、游戏模块、状态机等基础系统
    UI/               UI 窗口、层级、栈式导航与管理器
    Data/             配置表加载、配置管理
    Network/          TCP 连接、封包、Protobuf、消息分发
  GameLogic/          游戏业务逻辑与网络消息处理器
  GameRuntime/        .NET 独立运行入口，用于本地验证框架行为
  UnityClient/        Unity 工程目录，包含 Assets/Packages/ProjectSettings 与生成代码/数据

public/
  config/             策划配置源数据，按 client/server 等目标拆分
  proto/              Protobuf 协议定义与 proto.id 消息号映射
  tools/              Python 工具链：配置生成、配置导出、协议生成、数据校验

config/               旧本地导出目录；运行时 .cfgb 默认提交到 Unity StreamingAssets
server/               服务端预留目录
tools/                仓库级初始化/构建脚本
```

## 技术基线

- C# 框架层使用 `.NET Standard 2.1`，以便后续被 Unity 项目引用。
- 本地独立运行示例使用 `.NET 8`。
- C# 语言版本当前为 `10`，开启 Nullable。
- Protobuf C# 依赖 `Google.Protobuf`。
- 配置与协议生成工具使用 Python，依赖见 `public/tools/requirements.txt`。

## 常用命令

优先使用根目录初始化脚本，它会检查目录、校验配置、运行代码生成，并在可用时构建解决方案：

```powershell
.\init.bat
```

等价 PowerShell 命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\init.ps1
```

跳过部分步骤时可使用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\init.ps1 -SkipCodegen
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\init.ps1 -SkipDotnetBuild
```

单独构建：

```powershell
dotnet build .\client\GameFramework.sln
```

独立运行入口：

```powershell
dotnet run --project .\client\GameRuntime\
```

## 编码约定

- 优先沿用现有命名空间和目录结构，不为单个需求引入新的顶层架构。
- 框架通用能力放在 `client/GameFramework`，业务组合和具体处理放在 `client/GameLogic`。
- 保持 `GameFramework` 对 Unity 友好：避免直接依赖 UnityEngine，除非进入 Unity 专用适配层。
- 不要把所有能力都强行抽象进 `GameFramework`；如果某个能力放在 Unity 工程内更自然、更易维护，就应放在 Unity 侧。
- `GameFramework` 应提供稳定边界、通用接口和生命周期，不应承载大量 Unity 具体实现细节。
- 核心模块应通过明确生命周期工作，例如 Initialize、Update、Shutdown。
- 对外 API 尽量类型安全，避免用字符串作为核心调度键；已有协议 ID、配置类型、事件类型应优先复用。
- 新增公共模块时要考虑是否需要：
  - 独立初始化顺序或 Update 优先级
  - 生命周期释放
  - 日志/错误处理策略
  - 在 `GameRuntime` 中提供最小验证入口

## 已有核心模块

- `Core/EventSystem`：类型安全事件分发，包含 `IEvent`、`GameEvent<T>`、`EventDispatcher`、`EventBus`。
- `Core/GameSystem`：应用主循环、模块生命周期、状态机、可更新接口。
- `Core/ServiceLocator`：轻量服务注册与获取。
- `UI`：窗口生命周期、UI 层级和栈式窗口管理。
- `Data`：配置表、配置读取器、配置管理器以及生成的配置代码。
- `Network`：TCP 连接、封包编解码、Protobuf 消息、消息分发和生成协议枚举。

## 配置系统约定

配置源数据应放在 `public/config/client` 或后续对应目标目录中。Excel 表约定：

```text
Row 1: 字段名
Row 2: 字段类型，如 int/string/float/bool
Row 3: 导出目标，C/S/CS/X，空值等价 CS
Row 4: 注释说明
Row 5+: 数据
```

- `C` 表示仅客户端导出。
- `S` 表示仅服务端导出。
- `CS` 或空值表示客户端和服务端都导出。
- `X` 表示注释列，不生成代码、不导出数据。
- 生成的 C# 配置代码放在 `client/UnityClient/Assets/Scripts/Generated/Data`。
- 运行时二进制配置默认导出到 `client/UnityClient/Assets/StreamingAssets/Config`。
- Excel 源表、生成的 C# 配置代码和导出的 `.cfgb` 都应按职责提交：修改表结构时提交源表和生成代码/数据；只修改表数据时提交源表和 `.cfgb`。
- 不要手写修改生成文件；应修改源表或代码生成器，再重新生成。

## 协议系统约定

- 协议定义位于 `public/proto/*.proto`。
- 消息 ID 统一维护在 `public/proto/proto.id`。
- `CG_` 前缀表示 Client to GameServer。
- `GC_` 前缀表示 GameServer to Client。
- 生成产物包括：
  - `client/UnityClient/Assets/Scripts/Generated/Network/Protobuf/Generated/*.cs`
  - `client/UnityClient/Assets/Scripts/Generated/Network/Protobuf/EProtocol.cs`
  - `client/UnityClient/Assets/Scripts/Generated/Network/Handlers/*Handler.Generated.cs`
- 不要直接手写修改 Protobuf 生成类；应修改 `.proto`、`proto.id` 或生成器。
- 对 `GC_` 消息新增处理时，优先在 `GameLogic/Network/Handlers` 中补非生成 partial 业务处理。

## Unity 接入方向

当前 `client/UnityClient` 是预留目录。后续真正接入 Unity 时：

- 保持 `GameFramework` 为可被 Unity 引用的纯 C# 框架层。
- Unity 相关启动、MonoBehaviour 桥接、资源加载适配、场景生命周期适配应放入 Unity 工程侧。
- 不要让通用框架层直接耦合 Unity 场景、Prefab、Addressables 或编辑器 API。
- 可以新增 Unity 适配层，将 Unity 的 Update/LateUpdate/Application Quit 转发到 `GameApp` 或对应模块。
- 对 Resources、Addressables、AssetBundle、HybridCLR、Input System、AudioMixer 等 Unity 生态能力，优先在 Unity 工程侧实现适配器；只有稳定且跨后端通用的抽象才下沉到 `GameFramework`。
- 热更新相关设计应让 `GameFramework` 作为稳定宿主层，热更业务代码放在 `GameLogic`、`GameHotfix` 或 Unity 工程侧的热更程序集内。

## 工具链约定

- Python 工具位于 `public/tools`，按职责拆分到 `codegen` 和 `data`。
- 对生成器做改动后，应运行 `.\init.bat` 或至少运行对应生成命令，再构建 C# 解决方案。
- 工具输出应稳定，避免无意义重排导致生成文件频繁变更。
- 新增工具时，优先接入 `tools/init.ps1`，让根目录初始化流程成为统一入口。

## 验证要求

完成代码改动后，尽量执行：

```powershell
dotnet build .\client\GameFramework.sln
```

涉及配置、协议或生成器时，执行：

```powershell
.\init.bat
```

如果环境缺少 Python 包、.NET SDK 或 Unity 编辑器，应在最终说明中明确未验证项和原因。

## 后续扩展建议

适合逐步补齐的模块包括：

- 日志系统：统一日志等级、输出目标、Unity 控制台适配。
- 资源系统：本地资源、Addressables 或 AssetBundle 的抽象接口。
- 场景系统：加载、切换、预加载与生命周期事件。
- 时间系统：暂停、缩放、定时器、帧驱动任务。
- 输入系统：Unity Input System 适配与业务命令映射。
- 音频系统：BGM、音效、分组音量、淡入淡出。
- 存档系统：本地持久化、版本迁移、加密/校验。
- 热更新边界：如果引入，应先明确代码、资源、配置分别如何更新。

新增模块时，请先放入最小可运行骨架，并在 `GameRuntime` 或测试中留下验证方式。

## 给编码代理的工作原则

- 修改前先阅读相关模块，不要凭空替换已有架构。
- 优先小步提交清晰改动，避免一次性重写多个系统。
- 保留用户已有改动，不要执行破坏性 git 操作。
- 生成文件和源文件要区分对待：能从源生成的内容优先改源。
- 发现编码乱码时，不要扩大化重写；只在任务需要时修复相关文件。
- 最终回复应说明改了哪些文件、如何验证，以及仍需用户决策的事项。
