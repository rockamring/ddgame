# 协议消息说明

## 消息命名约定

| 前缀 | 方向 | 说明 |
|------|------|------|
| `CG_` | 客户端 → 服务器 | 请求消息，客户端直接发送，不需注册处理器 |
| `GC_` | 服务器 → 客户端 | 推送消息，自动生成处理器桩 + 注册代码 |
| 无前缀 | 内嵌数据 | 不单独走网络（如 `PlayerInfo`） |

## 消息 ID

ID 定义在 `proto.id` 中（目录下所有 proto 共用）：

```
CG_Login = 1003
GC_Login = 1004
```

## 使用

```csharp
// 发送 CG_ 消息
networkManager.Send((ushort)EProtocol.CG_Login, new CG_Login { Account = "test" });

// 注册 GC_ 处理器（初始化时调用一次）
GameHandler.RegisterAll(networkManager);
// 在 GameHandler.cs 的 OnXxx() 中编写业务逻辑
```

## 生成命令

```bash
python public/tools/codegen/proto_codegen.py \
  --proto-dir . \
  --output-dir ../../client/GameFramework/Network/Protobuf/ \
  --handler-dir ../../client/GameLogic/Network/Handlers/
```
