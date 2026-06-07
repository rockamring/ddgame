# 策划配置表说明

## Excel 表格格式

| 行 | 内容 | 示例 |
|---|------|------|
| 第1行 | 字段名 | `id` `name` `type` |
| 第2行 | 类型 | `int` `string` `list<int>` |
| 第3行 | 元数据标记 | `CS` `C` `S` `X`（见下方说明） |
| 第4行 | 注释说明 | `物品ID` `物品名称` |
| 第5行+ | 数据 | `1001 金币 1` |

## 元数据标记（第3行）

| 标记 | 含义 |
|------|------|
| `CS`（或留空） | 客户端 + 服务端都生成 |
| `C` | 仅客户端 |
| `S` | 仅服务端 |
| `X` | **注释列**，不导出数据、不生成代码 |

## 生成命令

```bash
# 生成代码
python public/tools/codegen/config_codegen.py --input ItemConfig.xlsx --output-dir ../../client/GameFramework/Data/Generated

# 导出二进制
python public/tools/codegen/config_exporter.py --input ItemConfig.xlsx --output-dir ../../config --target client
```
