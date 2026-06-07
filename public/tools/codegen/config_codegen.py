#!/usr/bin/env python3
"""
策划配置表 -> C# 代码生成器。

Excel 表格格式：
  Row 1: 表头（字段名）
  Row 2: 类型（int/string/bool/list<int>/…）
  Row 3: 元数据标记（C=仅客户端 S=仅服务端 CS=都生成 或 留空=都生成）
  Row 4: 注释说明
  Row 5+: 数据行

用法：
  python config_codegen.py --input ItemConfig.xlsx --output-dir ../client/GameFramework/Data/Generated/
  python config_codegen.py --input ItemConfig.xlsx --target client  # 只生成客户端字段
"""

import argparse
import json
import os
import re
import sys
from typing import Any, Dict, List, Optional, Tuple

try:
    import openpyxl
    HAS_OPENPYXL = True
except ImportError:
    HAS_OPENPYXL = False

# ============================================================
# 类型解析
# ============================================================

CS_TYPE_MAP: Dict[str, str] = {
    "int":    "int",
    "long":   "long",
    "float":  "float",
    "double": "double",
    "string": "string",
    "str":    "string",
    "bool":   "bool",
    "boolean":"bool",
}


def parse_cs_type(raw: str) -> str:
    """解析单元格中的类型字符串，返回 C# 类型名"""
    raw = raw.strip().lower()

    # 内置类型直接映射
    if raw in CS_TYPE_MAP:
        return CS_TYPE_MAP[raw]

    # 列表类型: list<int>, list<string>, list<long>, list<float>
    m = re.match(r"^list[<\[(](int|long|float|double|string|str)[>)\]]$", raw)
    if m:
        inner = CS_TYPE_MAP.get(m.group(1), m.group(1))
        return f"List<{inner}>"

    # 数组类型: int[], string[]
    m = re.match(r"^(int|long|float|double|string|str)\[\]$", raw)
    if m:
        inner = CS_TYPE_MAP.get(m.group(1), m.group(1))
        return f"{inner}[]"

    # 未知类型 —— 透传，留空则 fallback
    return raw if raw else "int"


# ============================================================
# 字符串工具
# ============================================================

def to_pascal_case(name: str) -> str:
    """蛇形/下划线命名 转 帕斯卡命名"""
    return "".join(word[0].upper() + word[1:] if word else ""
                   for word in re.split(r"[_\- ]", name))


def sanitize_field_name(name: str) -> str:
    name = re.sub(r"[^a-zA-Z0-9_]", "_", name)
    if name and name[0].isdigit():
        name = "_" + name
    return name or "_field"


def merge_metadata(meta_cell: Any) -> Dict[str, str]:
    """解析第 3 行元数据单元格，返回结构化信息"""
    result: Dict[str, str] = {}
    if meta_cell is None:
        return result
    raw = str(meta_cell).strip()

    # 可见性: C / S / CS（默认） / X（不导出）
    visibility = "CS"
    for v in ("CS", "BOTH", "C", "S", "X"):
        if raw.upper().startswith(v):
            if v == "BOTH":
                visibility = "CS"
            elif v == "X":
                visibility = "X"
            else:
                visibility = v
            break
    result["visibility"] = visibility

    # 范围: [...]
    m = re.search(r"[\[\(]([\d.]*)\s*[,:]\s*([\d.]*)[\]\)]", raw)
    if m:
        result["min"] = m.group(1)
        result["max"] = m.group(2)

    # 默认值
    m = re.search(r"default[=:]\s*(\S+)", raw, re.I)
    if m:
        result["default"] = m.group(1)

    return result


def should_emit(meta: Dict[str, str], target: str) -> bool:
    """根据目标平台判断字段是否应生成（X=不导出）"""
    vis = meta.get("visibility", "CS")
    if vis == "X":
        return False
    if target == "client":
        return vis in ("C", "CS")
    if target == "server":
        return vis in ("S", "CS")
    return True  # both


# ============================================================
# 代码生成
# ============================================================

def generate_row_class(table_name: str, fields: List[Dict]) -> str:
    """生成 C# 数据行类 (Config_xxx)"""
    class_name = f"Config_{to_pascal_case(table_name)}"
    lines = [
        "using System.Collections.Generic;",
        "",
        "namespace GameFramework.Data.Generated",
        "{",
        f"    /// <summary>",
        f"    /// 自动生成的配置行类 - {table_name}",
        f"    /// </summary>",
        f"    public class {class_name} : IConfigRow",
        "    {",
    ]

    has_id = False
    for i, f in enumerate(fields):
        cs_type = f["cs_type"]
        cs_name = to_pascal_case(f["name"])
        comment = f.get("comment", "")
        default = f.get("default", "")

        # id 字段映射到 _id 字段，通过 IConfigRow.Id 暴露
        if f["name"] == "id":
            has_id = True
            if comment:
                lines.append(f"        /// <summary>\n        /// {comment}\n        /// </summary>")
            lines.append(f"        [FieldIndex({i})]")
            lines.append(f"        public {cs_type} _id {{ get; set; }}")
            continue

        if comment:
            lines.append(f"        /// <summary>\n        /// {comment}\n        /// </summary>")
        lines.append(f"        [FieldIndex({i})]")
        if default:
            lines.append(f"        public {cs_type} {cs_name} {{ get; set; }} = {default};")
        else:
            lines.append(f"        public {cs_type} {cs_name} {{ get; set; }}")

    lines.append("")
    if has_id:
        lines.append("        public int Id => _id;")
    else:
        lines.append("        public int Id { get; set; }")
    lines.append("    }")
    lines.append("}")

    return "\n".join(lines)


# generate_table_class and generate_facade_class removed:
# 通用门面由框架 Config<T> 提供，不再为每张表重复生成。


# ============================================================
# Excel 处理
# ============================================================

def read_excel_rows(file_path: str) -> List[List[Any]]:
    """从 xlsx 读取有效行（跳过完全空行），返回列表"""
    wb = openpyxl.load_workbook(file_path, read_only=True, data_only=True)
    sheet = wb.active
    if sheet is None:
        print(f"[ERR] {file_path}: 没有工作表")
        wb.close()
        return []

    all_rows: List[List[Any]] = []
    for row in sheet.iter_rows(values_only=True):
        vals = list(row)
        if all(c is None for c in vals):
            continue
        all_rows.append(vals)

    wb.close()
    return all_rows


def process_excel_config(file_path: str, output_dir: str, target: str = "client") -> None:
    """
    处理 Excel 配置文件。
    布局: Row1=表头, Row2=类型, Row3=元数据标记, Row4=注释, Row5+=数据
    """
    if not HAS_OPENPYXL:
        print("[ERR] 需要 openpyxl 库: pip install openpyxl")
        sys.exit(1)

    all_rows = read_excel_rows(file_path)
    if len(all_rows) < 4:
        print(f"[ERR] {file_path}: 至少需要 4 行（表头+类型+元数据+注释），实际 {len(all_rows)} 行")
        return

    table_name = os.path.splitext(os.path.basename(file_path))[0]

    # ---- 解析行 ----
    headers = [str(c).strip() if c is not None else f"field_{i}"
               for i, c in enumerate(all_rows[0])]
    type_strs = [str(c).strip() if c is not None else "" for c in all_rows[1]]
    meta_strs = [c for c in all_rows[2]] if len(all_rows) > 2 else [None] * len(headers)
    comment_strs = [str(c).strip() if c is not None else "" for c in all_rows[3]] if len(all_rows) > 3 else [""] * len(headers)
    data_rows = all_rows[4:] if len(all_rows) > 4 else []

    num_cols = len(headers)

    # ---- 提取字段定义 ----
    fields: List[Dict] = []
    for ci in range(num_cols):
        meta = merge_metadata(meta_strs[ci] if ci < len(meta_strs) else None)

        # 根据目标平台过滤
        if not should_emit(meta, target):
            continue

        field = {
            "name": headers[ci],
            "cs_type": parse_cs_type(type_strs[ci] if ci < len(type_strs) else ""),
            "comment": comment_strs[ci] if ci < len(comment_strs) else "",
        }

        # 从元数据中提取默认值
        if "default" in meta:
            field["default"] = meta["default"]

        fields.append(field)

    # ---- 生成代码（只生成 Row 类，通用门面由框架 Config<T> 提供） ----
    row_class_name = f"Config_{to_pascal_case(table_name)}"

    os.makedirs(output_dir, exist_ok=True)

    row_code = generate_row_class(table_name, fields)
    row_path = os.path.join(output_dir, f"{row_class_name}.cs")
    with open(row_path, "w", encoding="utf-8") as f:
        f.write(row_code)

    print(f"[OK] 生成: {row_path}")
    print(f"     - 字段数: {len(fields)}")
    print(f"     - 数据行: {len(data_rows)}")
    print(f"     - 目标: {target}")


# ============================================================
# JSON 处理（兼容）
# ============================================================

def process_json_config(file_path: str, output_dir: str) -> None:
    """兼容处理 JSON 配置"""
    with open(file_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    if not isinstance(data, list) or not data:
        print(f"[WARN] {file_path}: 空数据或不是数组")
        return

    table_name = os.path.splitext(os.path.basename(file_path))[0]

    # 从第一条记录推断字段类型
    first = data[0]
    fields = []
    for key in first:
        vals = [row.get(key) for row in data if key in row]
        # simple type inference
        py_types = {type(v).__name__ for v in vals if v is not None}
        if "str" in py_types:
            cs = "string"
        elif "float" in py_types:
            cs = "float"
        elif "bool" in py_types:
            cs = "bool"
        else:
            cs = "int"
        fields.append({"name": key, "cs_type": cs, "comment": ""})

    row_cls = f"Config_{to_pascal_case(table_name)}"

    os.makedirs(output_dir, exist_ok=True)

    with open(os.path.join(output_dir, f"{row_cls}.cs"), "w", encoding="utf-8") as f:
        f.write(generate_row_class(table_name, fields))

    print(f"[OK] JSON -> {row_cls}.cs")
    print(f"     - 字段数: {len(fields)}, 数据行: {len(data)}")


# ============================================================
# CLI
# ============================================================

def main():
    parser = argparse.ArgumentParser(description="配置表 -> C# 代码生成器")
    parser.add_argument("--input", "-i", required=True, help="输入文件 (.xlsx / .json)")
    parser.add_argument("--output-dir", "-o", default="client/GameFramework/Data/Generated", help="输出目录（默认 client/GameFramework/Data/Generated）")
    parser.add_argument("--target", choices=["client", "server", "all"], default="client",
                        help="目标平台（默认 client，按元数据过滤字段）")

    args = parser.parse_args()

    if not os.path.exists(args.input):
        print(f"[ERR] 输入文件不存在: {args.input}")
        sys.exit(1)

    ext = os.path.splitext(args.input)[1].lower()

    if ext == ".xlsx":
        process_excel_config(args.input, args.output_dir, args.target)
    elif ext == ".json":
        process_json_config(args.input, args.output_dir)
    else:
        print(f"[ERR] 不支持的文件格式: {ext}（支持 .xlsx .json）")
        sys.exit(1)


if __name__ == "__main__":
    main()
