# Game Client Framework

游戏客户端架构框架，从零构建。

## 项目结构

```
game/
├── client/                    ← 客户端 C# 代码
│   ├── GameFramework/         ← 核心框架 (.NET Standard 2.1)
│   │   ├── Core/              ← 核心模块 (Event/Game/Service)
│   │   ├── UI/                ← UI系统
│   │   ├── Data/              ← 策划数据系统
│   │   └── Network/           ← 网络层 (Protobuf + TCP)
│   ├── GameGenerated/         ← 本地验证用生成代码项目（引用 Unity 生成目录）
│   ├── GameLogic/             ← 游戏逻辑
│   ├── GameRuntime/           ← 独立运行入口 (.NET 8)
│   └── UnityClient/           ← Unity 项目与生成代码/运行时数据
│
├── server/                    ← (预留) 服务端代码
│
└── public/                    ← 公共资源
    ├── tools/                 ← Python 工具链
    ├── config/                ← 策划配置数据
    └── proto/                 ← Protobuf 协议定义
```

## 核心模块

### EventSystem（事件系统）
- 类型安全的事件分发（`IEvent` + `GameEvent<T>`）
- 优先级排序 + 一次性监听
- 全局 `EventBus` 静态入口

### GameSystem（游戏系统）
- `GameApp` 单例主循环
- `GameModule` 模块生命周期
- `GameStateMachine` 状态机
- `ServiceLocator` 服务容器

### UISystem（UI系统）
- `UIManager` 窗口管理
- `UIWindow` 窗口生命周期（Init → Open → Close → Destroy）
- 多层栈式导航（`UIStack`）

### DataSystem（策划数据流）
- `ConfigTable<T>` 泛型配置表
- 代码生成：Excel → C#（Python 工具链）

**Excel 表格约定格式：**
```
Row 1: 表头（字段名）       id  |  name  |  type  |  quality
Row 2: 类型（int/string）  int | string |  int   |  int
Row 3: 元数据标记         CS  |  CS    |  C     |  S
Row 4: 注释说明           唯一ID | 物品名 | 客户端 | 服务端
Row 5+: 数据行            1001 | 金币  |  1     |  3
```
- `C` = 仅客户端  `S` = 仅服务端  `CS`(或空) = 都生成  `X` = 注释列（不导出、不生成代码）
- `CS` 是默认值，元数据格留空等价于 `CS`
- 使用 `--target client` 或 `--target server` 按平台过滤

### 使用方法

自动加载（文件命名约定：`Config_ItemConfig` → `ItemConfig.cfgb`）：
```csharp
// 按 ID 查找 —— 首次访问自动加载
var item = DataManager.Get<Config_ItemConfig>(1001);
string name = item.Name;

// 遍历
foreach (var item in DataManager.All<Config_ItemConfig>()) { ... }

// 其他查询
bool found = DataManager.TryGet<Config_ItemConfig>(2001, out var row);
bool exists = DataManager.Contains<Config_ItemConfig>(3001);
int count = DataManager.Count<Config_ItemConfig>();
```

手动指定路径加载：
```csharp
DataManager.Load<Config_ItemConfig>("config/ItemConfig.cfgb");
DataManager.LoadFromBytes<Config_ItemConfig>(binaryData);
```

配置目录默认为 `config/`；Unity 启动时会设置为 `Application.streamingAssetsPath/Config`：
```csharp
DataManager.ConfigDirectory = "./config_data";
```

### NetworkSystem（网络层）
- TCP 异步连接（自动重连）
- Protobuf 消息序列化
- `MessageDispatcher` 消息路由
- `[MessageHandler]` 属性标记

### Python 工具链
| 工具 | 功能 |
|------|------|
| `config_codegen.py` | Excel/JSON 配置表 → C# 代码 |
| `config_exporter.py` | Excel 配置表 → 二进制 .cfgb 文件（运行时加载） |
| `proto_codegen.py`  | .proto → C# 枚举 + 注册代码 |
| `validators.py`     | 数据校验 |

## 数据流

```
策划定义 Excel 表格
    ↓
config_codegen.py  ──→ C# 代码 (client/UnityClient/Assets/Scripts/Generated/Data)
config_exporter.py ──→ 二进制文件 (client/UnityClient/Assets/StreamingAssets/Config)
    ↓
游戏启动 → DataManager 自动加载 ItemConfig.cfgb
         → DataManager.Get<Config_ItemConfig>(id) 直接查找
```

## 二进制格式 (.cfgb)

自定义紧凑格式，无字段元数据，代码和数据严格配对：

```
[4B MAGIC "CFGB"] [4B 行数] [逐行字段值 packed]
```

C# 侧通过 `[FieldIndex(N)]` 属性标注字段顺序，反射按序列读取。

## 快速开始

### 0. 初始化工具链
```bat
init.bat
```

或：
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\init.ps1
```

### 1. 生成 C# 配置代码（Excel → C#）
```bash
pip install openpyxl
python public/tools/codegen/config_codegen.py \
  --input public/config/client/ItemConfig.xlsx \
  --output-dir client/UnityClient/Assets/Scripts/Generated/Data
```

### 1b. 导出二进制数据（Excel → .cfgb）
```bash
python public/tools/codegen/config_exporter.py \
  --input public/config/client/ItemConfig.xlsx \
  --output-dir client/UnityClient/Assets/StreamingAssets/Config \
  --target client
```

### 2. 生成协议代码
```bash
python public/tools/codegen/proto_codegen.py \
  --proto-dir public/proto/ \
  --output-dir client/UnityClient/Assets/Scripts/Generated/Network/Protobuf/ \
  --handler-dir client/UnityClient/Assets/Scripts/Generated/Network/Handlers/
```

### 3. 数据校验
```bash
python public/tools/data/validators.py --config-dir public/config/client/
```

### 4. 构建运行（需 .NET 8 SDK）
```bash
dotnet build client/GameFramework.sln
dotnet run --project client/GameRuntime/
```

## 协议格式

```
 [4字节:总包长] [2字节:消息ID] [Protobuf消息体]
```

### 消息 ID 定义

消息 ID 统一定义在 `proto.id` 文件中（目录下所有 proto 共用一份），前后端共用保证 ID 严格匹配：

```
# public/proto/proto.id
CG_Heartbeat = 1000
GC_Heartbeat = 1001
GC_ErrorNotify = 1002
CG_Login = 1003
GC_Login = 1004
GC_PlayerDataSync = 1005
```

### 消息命名约定

| 前缀 | 方向 | 客户端行为 |
|------|------|-----------|
| `CG_` | 客户端→服务器 | 直接发送，无需注册处理器 |
| `GC_` | 服务器→客户端 | 自动生成处理器注册与转发 |

示例：
- 客户端发送 `CG_Login` → 服务端处理登录
- 服务端返回 `GC_Login` → 自动转发到 `GameHandler` 的 partial 业务实现

### 代码生成

```bash
python public/tools/codegen/proto_codegen.py \
  --proto-dir public/proto/ \
  --output-dir client/UnityClient/Assets/Scripts/Generated/Network/Protobuf/ \
  --handler-dir client/UnityClient/Assets/Scripts/Generated/Network/Handlers/
```

生成产物：
- `Network/Protobuf/Generated/Game.cs` — 消息类（protoc 生成，Google.Protobuf.IMessage 实现）
- `Network/Protobuf/EProtocol.cs` — 消息 ID 枚举
- `Network/Handlers/GameHandler.Generated.cs` — `GC_` 消息注册与转发

### 使用

发送 `CG_` 消息：
```csharp
var msg = new CG_Heartbeat { ClientTime = 12345 };
networkManager.Send((ushort)EProtocol.CG_Heartbeat, msg);
```

注册处理器（一次初始化，自动绑定所有 `GC_` 回调）：
```csharp
GameHandler.RegisterAll(networkManager);
```

处理 `GC_` 消息时，编辑 `client/GameLogic/Network/Handlers/GameHandler.cs` 中对应 partial 方法。
