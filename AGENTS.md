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
  GameFramework/      .NET Standard 构建壳，链接编译 UnityClient/Assets/Scripts/Framework 下的框架源码
  UnityClient/        Unity 工程目录，包含 Assets/Packages/ProjectSettings、框架源码、生成代码与运行时数据
    Assets/Scripts/Framework/
      Core/           事件、服务定位、游戏模块、状态机等基础系统
      UI/             UI 窗口、层级、栈式导航与管理器
      Data/           配置表加载、配置管理
      Network/        TCP 连接、封包、Protobuf、消息分发
      Logging/        日志系统
      Save/           本地存档
      Time/           定时器/时间系统
      Resource/       Unity 侧资源加载、Provider、缓存与引用计数

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
```

单独构建：

```powershell
```


## 编码约定

- 优先沿用现有命名空间和目录结构，不为单个需求引入新的顶层架构。
- 框架通用能力放在 `client/UnityClient/Assets/Scripts/Framework`，业务组合和具体处理放在 `client/UnityClient/Assets/Scripts/GameLogic`。
- 保持 `GameFramework` 对 Unity 友好：避免直接依赖 UnityEngine，除非进入 Unity 专用适配层。
- 不要把所有能力都强行抽象进 `GameFramework`；如果某个能力放在 Unity 工程内更自然、更易维护，就应放在 Unity 侧。
- `GameFramework` 应提供稳定边界、通用接口和生命周期，不应承载大量 Unity 具体实现细节。
- 核心模块应通过明确生命周期工作，例如 Initialize、Update、Shutdown。
- 对外 API 尽量类型安全，避免用字符串作为核心调度键；已有协议 ID、配置类型、事件类型应优先复用。
- 新增公共模块时要考虑是否需要：
  - 独立初始化顺序或 Update 优先级
  - 生命周期释放
  - 日志/错误处理策略
  - 在 Unity 侧提供最小验证入口

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
- 对 `GC_` 消息新增处理时，优先在 `client/UnityClient/Assets/Scripts/GameLogic/Network/Handlers` 中补非生成 partial 业务处理。

## Unity 接入方向

当前 `client/UnityClient` 是预留目录。后续真正接入 Unity 时：

- 保持 `GameFramework` 为可被 Unity 引用的纯 C# 框架层。
- Unity 相关启动、MonoBehaviour 桥接、资源加载适配、场景生命周期适配应放入 Unity 工程侧。
- 不要让通用框架层直接耦合 Unity 场景、Prefab、Addressables 或编辑器 API。
- 可以新增 Unity 适配层，将 Unity 的 Update/LateUpdate/Application Quit 转发到 `GameApp` 或对应模块。
- 对 Resources、Addressables、AssetBundle、HybridCLR、Input System、AudioMixer 等 Unity 生态能力，优先在 Unity 工程侧实现适配器；只有稳定且跨后端通用的抽象才下沉到 `GameFramework`。
- 热更新相关设计应让 `Framework` 作为稳定宿主层，热更业务代码放在 `GameLogic`、`GameHotfix` 或 Unity 工程侧的热更程序集内。

## 工具链约定

- Python 工具位于 `public/tools`，按职责拆分到 `codegen` 和 `data`。
- 对生成器做改动后，应运行 `.\init.bat` 或至少运行对应生成命令，再构建 C# 解决方案。
- 工具输出应稳定，避免无意义重排导致生成文件频繁变更。
- 新增工具时，优先接入 `tools/init.ps1`，让根目录初始化流程成为统一入口。

## 验证要求

完成代码改动后，尽量执行：

```powershell
```

涉及配置、协议或生成器时，执行：

```powershell
.\init.bat
```

如果环境缺少 Python 包、.NET SDK 或 Unity 编辑器，应在最终说明中明确未验证项和原因。

## 开发计划与当前状态

状态说明：

- `已完成`：已有最小可运行骨架，并能通过 Unity 入口或生成流程验证。
- `进行中`：已有基础实现，但接口、Unity 适配、错误处理或工具链还需要补齐。
- `下一步`：建议优先进入近期迭代的模块。
- `待规划`：需要先确定业务形态、Unity 技术选型或编辑器工作流，再进入编码。
- `低优先级`：当前框架骨架阶段暂不阻塞其它模块，可后置。

| 模块/方向 | 状态 | 已有基础 | 下一步工作 |
| --- | --- | --- | --- |
| 游戏主循环、中枢系统 | 已完成 | `GameApp`、`GameModule`、`IUpdateable`、`GameStateMachine` 已提供初始化、Update、Shutdown、模块优先级与服务注册；Unity `GameBootstrap` 已可驱动。 | 补充模块依赖/初始化顺序约束、异常隔离、暂停/恢复策略；生命周期日志已接入 `LoggerManager`，后续可继续增强诊断信息。 |
| 事件分发系统 | 已完成 | `IEvent`、`GameEvent<T>`、`EventDispatcher`、`EventBus` 已支持类型安全分发、优先级和一次性监听。 | 增加事件调试信息、可选的异步/延迟派发队列、监听泄漏检测。 |
| 服务定位 | 已完成 | `ServiceLocator` 已支持轻量服务注册与获取，`GameApp` 会注册模块服务。 | 明确服务覆盖、卸载和测试替换策略；必要时增加只读查询接口。 |
| UI 系统 | 进行中 | `UIWindow`、`UILayer`、`UIStack`、`UIManager` 已支持窗口生命周期、层级和栈式管理。 | 接入 Unity UI 适配层，补窗口 prefab/资源加载绑定、遮罩/焦点/返回键规则；后续再规划 MVVM/MVP、UI 代码生成和可视化工具。 |
| 策划数据导出、代码生成、数据加载 | 已完成 | `public/config`、`config_codegen.py`、`config_exporter.py`、`DataManager`、生成的 `Config_ItemConfig` 与 `.cfgb` 已形成 Excel 到运行时数据链路。 | 扩展字段类型、默认值、数组/枚举/引用校验；生成器输出稳定性和错误提示继续增强。 |
| Protobuf 协议、代码生成、服务器连接、消息处理 | 已完成 | `public/proto`、`proto.id`、`proto_codegen.py`、`TcpConnection`、`PacketCodec`、`NetworkManager`、`MessageDispatcher` 和生成 handler 已打通基础流程。 | 补断线重连策略、心跳超时、请求响应关联、错误码规范、网络状态事件到 UI/业务层的桥接。 |
| 资源加载模块 | 进行中 | 资源系统主体已移动到 Unity 侧，`ResourceManager`、`IResourceProvider`、`ResourceHandle` 和 `ResourcesProvider` 已支持同步/异步加载、Provider 优先级、缓存、引用计数与释放 Token。 | 接入 Addressables Provider，统一资源 ID/路径生成，补预加载、分组释放、依赖统计、加载进度回调和 UniTask 版本接口。 |
| Unity 启动与框架桥接 | 进行中 | `GameBootstrap` 已在 Unity 启动前创建宿主，注册默认模块，转发 `Update` 并在退出时 Shutdown；默认接入 `LoggerManager`、`TimerManager`、`SaveManager`、`UnityLogSink` 和 Unity 持久化目录。 | 补 LateUpdate/FixedUpdate 可选转发、场景切换时的生命周期策略、编辑器下重复初始化保护和启动配置资产。 |
| 客户端数据本地持久化 | 已完成 | `SaveManager`、`IStorageProvider`、`LocalFileStorageProvider` 已支持文本/二进制读写、存在检测、删除和根目录逃逸保护；Unity 默认写入 `Application.persistentDataPath/SaveData`。 | 后续补 JSON/二进制对象序列化、版本迁移、校验/加密、云存档或多槽位策略。 |
| 定时器/时间系统 | 已完成 | `TimerManager` 已支持一次性定时器、循环定时器、取消、清理、暂停、时间缩放和忽略缩放；Unity 校验组件已接入。 | 后续补调试面板、按标签批量取消、协程/Task 风格等待接口。 |
| 场景加载 | 下一步 | 暂无独立模块。 | 在 Unity 侧实现 SceneService/SceneModule，支持异步加载、切换、预加载、进度事件和场景生命周期事件。 |
| 输入系统 | 下一步 | 暂无独立模块。 | 优先在 Unity 侧接入 Input System，把输入映射为业务命令或事件；框架层只保留稳定抽象。 |
| 音频系统 | 下一步 | 暂无独立模块。 | 在 Unity 侧实现 AudioService，管理 BGM、音效、分组音量、静音、淡入淡出和 AudioMixer 适配。 |
| 日志系统 | 已完成 | `LoggerManager`、`Log`、`ILogSink`、`ConsoleLogSink`、`UnityLogSink` 已支持等级、分类、异常输出、多输出目标和 Unity Console 适配。 | 后续补文件日志、远端日志、运行时开关、采样/限流和日志面板。 |
| 特效播放 | 待规划 | 暂无独立模块。 | 依赖资源系统和对象池；先定义 EffectService，支持播放、挂点、生命周期、回收和 Addressables 加载。 |
| 动画模块 | 待规划 | 暂无独立模块。 | 先明确 Animator/Playable/Timeline 使用边界；Unity 侧优先提供适配，不下沉 Unity 细节到 `GameFramework`。 |
| 技能模块 | 待规划 | 暂无独立模块。 | 先设计技能数据结构、运行时上下文、目标选择、效果结算和表现分离；编辑器工具后置，避免过早绑定具体玩法。 |
| TrackView/剧情编辑系统 | 待规划 | 暂无独立模块。 | 先调研 Unity Timeline/Playable 复用方式；定义剧情轨道数据、触发条件、跳过/回放/本地化边界。 |
| 资源热更新模块 | 待规划 | 暂无独立模块。 | 先明确 Addressables/AssetBundle 方案、版本清单、差异下载、校验、回滚和 CDN 路径规范。 |
| C# 代码热更新模块 | 待规划 | 暂无独立模块。 | 先确定 HybridCLR/ILRuntime 等方案；划清稳定宿主层、热更程序集、生成代码和 AOT 补充元数据边界。 |
| Unity 打包、更新工具与流水线 | 待规划 | 已有 `tools/init.ps1` 负责初始化、生成和构建 C#。 | 后续补 Unity Editor 打包脚本、命令行构建、渠道参数、版本号、资源构建、补丁生成和 CI 入口。 |
| 可编程渲染修改 | 待规划 | Unity 工程已有基础 ProjectSettings。 | 先确定 URP/HDRP/内置管线目标；渲染改动应放 Unity 工程侧，并以具体效果需求驱动。 |
| 游戏 GameObject 体系 | 低优先级 | 暂无独立模块。 | 针对具体游戏类型再设计实体/组件/表现/逻辑结构；当前不先抽象通用 GameObject 框架。 |

## 近期建议迭代顺序

1. 完成 Unity 侧 `Addressables` 资源 Provider、场景加载服务和输入/音频适配，让 Unity 工程具备真实可运行闭环。
2. 增强网络层的心跳、重连、错误码和请求响应模式，为后续登录、角色数据同步等业务打底。
3. 在 UI 资源加载链路稳定后，再推进 UI MVVM/MVP、窗口代码生成和工具化配置。
4. 为 `Logger`、`Timer`、`SaveManager` 补更细的运行时调试面板、文件日志、对象序列化、存档版本迁移等增强能力。
5. 在资源系统、配置系统、网络系统稳定后，再规划热更新、技能编辑、TrackView 和打包流水线等重工具化模块。

新增模块时，请先放入最小可运行骨架，并在 Unity 侧留下验证方式；涉及 Unity 生态能力时优先放在 `client/UnityClient/Assets/Scripts/Framework` 的对应模块或适配层。

## 给编码代理的工作原则

- 修改前先阅读相关模块，不要凭空替换已有架构。
- 优先小步提交清晰改动，避免一次性重写多个系统。
- 保留用户已有改动，不要执行破坏性 git 操作。
- 生成文件和源文件要区分对待：能从源生成的内容优先改源。
- 发现编码乱码时，不要扩大化重写；只在任务需要时修复相关文件。
- 最终回复应说明改了哪些文件、如何验证，以及仍需用户决策的事项。
