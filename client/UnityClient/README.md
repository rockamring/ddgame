# UnityClient

Unity 客户端工程预留目录。

当前框架代码位于 `client/GameFramework` 和 `client/GameLogic`，目标框架为 `.NET Standard 2.1`，可作为 Unity 侧代码层接入。外围工具链位于 `public/tools`：

- 配置表：`public/config/client/*.xlsx` -> `client/GameFramework/Data/Generated/*.cs` + `config/*.cfgb`
- 协议：`public/proto/*.proto` + `public/proto/proto.id` -> Protobuf C# 代码 + 客户端处理器桩

项目根目录双击/执行：

```bat
init.bat
```

或手动执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\init.ps1
```

脚本会完成目录检查、配置校验、配置代码生成、配置数据导出、协议代码生成，并在检测到 .NET 8 SDK 时构建独立运行示例。
