#!/usr/bin/env python3
"""
策划配置表 -> 二进制 .cfgb 导出工具。

代码和数据严格配对：二进制文件中不存字段名/类型/版本号，
C# 侧通过 [FieldIndex] + 反射按列序读取。

流程:
  Excel → config_codegen.py（生成 C# 代码）
       → config_exporter.py（导出二进制数据，本工具）

用法：
  # 单文件导出
  python config_exporter.py --input ../../configs/ItemConfig.xlsx --output-dir ../../client/UnityClient/Assets/StreamingAssets/Config/

  # 导出整个目录
  python config_exporter.py --all --input-dir ../../configs/ --output-dir ../../client/UnityClient/Assets/StreamingAssets/Config/ --target client

  # JSON 转二进制（兼容已有数据）
  python config_exporter.py --json --input ../../configs/ItemConfig.json --output-dir ../../client/UnityClient/Assets/StreamingAssets/Config/
"""

import argparse
import json
import os
import re
import struct
import sys
from typing import Any, Dict, List, Optional, Tuple

try:
    import openpyxl
    HAS_OPENPYXL = True
except ImportError:
    HAS_OPENPYXL = False

# ============================================================
# 类型信息：Python type → type code → struct format
# ============================================================

TYPE_MAP: Dict[str, Tuple[str, int]] = {
    "int":    ("<i", 0),    # 4B signed LE
    "long":   ("<q", 1),    # 8B signed LE
    "float":  ("<f", 2),    # 4B IEEE 754 LE
    "double": ("<d", 3),    # 8B IEEE 754 LE
    "string": ("str", 4),   # [4B len][N utf8]
    "bool":   ("<?", 5),    # 1B (0/1)
    "EAssetId": ("<I", 12), # 4B unsigned LE (资源引用 ID)
    "list<int>":    ("list_i", 6),
    "list<long>":   ("list_q", 7),
    "list<float>":  ("list_f", 8),
    "list<double>": ("list_d", 9),
    "list<string>": ("list_str", 10),
    "list<bool>":   ("list_?", 11),
}

MAGIC = b"CFGB"

# EAssetId 临时分配（Phase 1 用计数器分配 ID）
# Phase 2 的 resource_codegen.py 会生成完整映射并重写此逻辑
_resource_ref_counter = 0
_resource_ref_map: Dict[str, int] = {}


def _resolve_asset_id(path: str) -> int:
    """将资源路径字符串转为稳定的 uint ID"""
    global _resource_ref_counter
    key = path.strip().lower()
    if not key:
        return 0
    if key not in _resource_ref_map:
        _resource_ref_counter += 1
        _resource_ref_map[key] = _resource_ref_counter
    return _resource_ref_map[key]


# ============================================================
# 二进制写入
# ============================================================

def _write_string(f, val: str) -> None:
    """写长度前缀 UTF8 字符串"""
    encoded = val.encode("utf-8")
    f.write(struct.pack("<I", len(encoded)))
    f.write(encoded)


def write_value(f, cs_type: str, val: Any) -> None:
    """按 C# 类型编码写入一个值到文件"""
    if val is None:
        val = "" if cs_type == "string" else 0

    if cs_type == "int":
        f.write(struct.pack("<i", int(val)))
    elif cs_type == "EAssetId":
        f.write(struct.pack("<I", _resolve_asset_id(str(val)) if val else 0))
    elif cs_type == "long":
        f.write(struct.pack("<q", int(val)))
    elif cs_type == "float":
        f.write(struct.pack("<f", float(val)))
    elif cs_type == "double":
        f.write(struct.pack("<d", float(val)))
    elif cs_type == "bool":
        f.write(struct.pack("<?", bool(val)))
    elif cs_type == "string":
        _write_string(f, str(val) if val is not None else "")
    elif cs_type.startswith("List<"):
        # 列表类型: list<int> → inner="int"
        inner = cs_type[5:-1]
        items = list(val) if isinstance(val, (list, tuple)) else []
        f.write(struct.pack("<I", len(items)))
        for item in items:
            write_value(f, inner, item)
    else:
        print(f"[WARN] 未知类型 '{cs_type}'，按 string 写入")
        _write_string(f, str(val) if val is not None else "")


def export_table(fields: List[Dict], data_rows: List[List[Any]], output_path: str) -> None:
    """将一张表导出为 .cfgb 二进制文件"""
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    with open(output_path, "wb") as f:
        # Magic — 文件类型校验
        f.write(MAGIC)
        # 行数
        f.write(struct.pack("<I", len(data_rows)))

        # 数据区：逐行、逐字段写入
        for row in data_rows:
            for field in fields:
                source_index = field.get("source_index")
                if source_index is None:
                    source_index = field.get("index", 0)
                val = row[source_index] if source_index < len(row) else None
                write_value(f, field["cs_type"], val)

    print(f"[OK] 导出: {output_path}")
    print(f"     行数: {len(data_rows)}, 字段: {len(fields)}")


# ============================================================
# Excel 读取（与 config_codegen.py 格式一致）
# ============================================================

def read_excel(file_path: str) -> Optional[Tuple[str, List[str], List[str], List[Any], List[List[Any]]]]:
    """
    读取 Excel 配置表。
    返回: (table_name, headers, type_strs, meta_strs, data_rows)
    """
    if not HAS_OPENPYXL:
        print("[ERR] 需要 openpyxl 库: pip install openpyxl")
        return None

    wb = openpyxl.load_workbook(file_path, read_only=True, data_only=True)
    ws = wb.active
    if ws is None:
        print(f"[ERR] {file_path}: 没有工作表")
        wb.close()
        return None

    all_rows: List[List[Any]] = []
    for row in ws.iter_rows(values_only=True):
        vals = list(row)
        if all(c is None for c in vals):
            continue
        all_rows.append(vals)
    wb.close()

    if len(all_rows) < 4:
        print(f"[ERR] {file_path}: 至少需要 4 行（表头+类型+元数据+注释），实际 {len(all_rows)} 行")
        return None

    table_name = os.path.splitext(os.path.basename(file_path))[0]
    headers = [str(c).strip() if c is not None else f"field_{i}"
               for i, c in enumerate(all_rows[0])]
    type_strs = [str(c).strip() if c is not None else "" for c in all_rows[1]]
    meta_strs = [c for c in all_rows[2]] if len(all_rows) > 2 else [None] * len(headers)
    data_rows = all_rows[4:] if len(all_rows) > 4 else []

    return table_name, headers, type_strs, meta_strs, data_rows


def parse_cs_type(raw: str) -> str:
    """解析类型字符串为 C# 类型名（与 config_codegen 一致）"""
    raw = raw.strip().lower()
    builtin = {"int": "int", "long": "long", "float": "float", "double": "double",
               "string": "string", "str": "string", "bool": "bool", "boolean": "bool",
               "resource_ref": "EAssetId"}
    if raw in builtin:
        return builtin[raw]

    m = re.match(r"^list[<\[(](int|long|float|double|string|str)[>)\]]$", raw)
    if m:
        inner = builtin.get(m.group(1), m.group(1))
        return f"List<{inner}>"

    m = re.match(r"^(int|long|float|double|string|str)\[\]$", raw)
    if m:
        inner = builtin.get(m.group(1), m.group(1))
        return f"{inner}[]"

    return raw if raw else "int"


def merge_metadata(meta_cell: Any) -> Dict[str, str]:
    """解析元数据单元格（与 config_codegen 一致）"""
    result: Dict[str, str] = {}
    if meta_cell is None:
        return result
    raw = str(meta_cell).strip()

    for v in ("CS", "BOTH", "C", "S", "X"):
        if raw.upper().startswith(v):
            if v == "BOTH":
                result["visibility"] = "CS"
            elif v == "X":
                result["visibility"] = "X"
            else:
                result["visibility"] = v
            break

    m = re.search(r"[\[\(]([\d.]*)\s*[,:]\s*([\d.]*)[\]\)]", raw)
    if m:
        result["min"] = m.group(1)
        result["max"] = m.group(2)

    m = re.search(r"default[=:]\s*(\S+)", raw, re.I)
    if m:
        result["default"] = m.group(1)

    return result


def should_emit(meta: Dict[str, str], target: str) -> bool:
    """根据目标平台判断字段是否生成（X=不导出）"""
    vis = meta.get("visibility", "CS")
    if vis == "X":
        return False
    if target == "client":
        return vis in ("C", "CS")
    if target == "server":
        return vis in ("S", "CS")
    return True


# ============================================================
# 核心导出函数
# ============================================================

def process_excel(file_path: str, output_dir: str, target: str = "client") -> None:
    """处理单个 Excel 文件，导出二进制"""
    result = read_excel(file_path)
    if result is None:
        return

    table_name, headers, type_strs, meta_strs, data_rows = result

    # 构建字段列表（过滤 + 类型解析）
    fields: List[Dict] = []
    for ci, h in enumerate(headers):
        meta = merge_metadata(meta_strs[ci] if ci < len(meta_strs) else None)
        if not should_emit(meta, target):
            continue
        fields.append({
            "name": h,
            "cs_type": parse_cs_type(type_strs[ci] if ci < len(type_strs) else ""),
            "source_index": ci,
        })

    if not fields:
        print(f"[WARN] {file_path}: 没有可导出的字段（target={target}）")
        return

    # 导出二进制
    output_path = os.path.join(output_dir, f"{table_name}.cfgb")
    export_table(fields, data_rows, output_path)


def process_json(file_path: str, output_dir: str) -> None:
    """将 JSON 配置转二进制（兼容已有数据）"""
    try:
        with open(file_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception as e:
        print(f"[ERR] JSON 读取失败: {e}")
        return

    if not isinstance(data, list) or not data:
        print(f"[ERR] {file_path}: 空数据或不是数组")
        return

    table_name = os.path.splitext(os.path.basename(file_path))[0]

    # 从第一条记录推断字段类型
    first = data[0]
    fields = []
    for key in first:
        vals = [row.get(key) for row in data if key in row]
        py_types = {type(v).__name__ for v in vals if v is not None}
        if "str" in py_types:
            cs = "string"
        elif "float" in py_types:
            cs = "float"
        elif "bool" in py_types:
            cs = "bool"
        else:
            cs = "int"
        fields.append({"name": key, "cs_type": cs, "source_index": len(fields)})

    # 将 JSON 行转为有序数据行
    data_rows = []
    for row in data:
        data_rows.append([row.get(f["name"]) for f in fields])

    output_path = os.path.join(output_dir, f"{table_name}.cfgb")
    export_table(fields, data_rows, output_path)


def process_directory(input_dir: str, output_dir: str, target: str = "client") -> None:
    """导出目录下所有 xlsx 配置"""
    for filename in sorted(os.listdir(input_dir)):
        ext = os.path.splitext(filename)[1].lower()
        if ext != ".xlsx":
            continue
        file_path = os.path.join(input_dir, filename)
        print(f"\n处理: {filename}")
        process_excel(file_path, output_dir, target)


# ============================================================
# CLI
# ============================================================

def main():
    parser = argparse.ArgumentParser(description="配置表 -> .cfgb 二进制导出")
    parser.add_argument("--input", "-i", help="输入文件 (.xlsx / .json)")
    parser.add_argument("--input-dir", "-d", help="输入目录（配合 --all）")
    parser.add_argument("--output-dir", "-o", default="client/UnityClient/Assets/StreamingAssets/Config", help="输出目录（默认 client/UnityClient/Assets/StreamingAssets/Config）")
    parser.add_argument("--target", choices=["client", "server", "all"], default="client",
                        help="目标平台（默认 client，按元数据过滤字段）")
    parser.add_argument("--all", "-a", action="store_true", help="批量导出目录下所有 xlsx")
    parser.add_argument("--json", action="store_true", help="输入为 JSON 格式")

    args = parser.parse_args()

    if args.all:
        if not args.input_dir:
            print("[ERR] --all 需要 --input-dir 指定输入目录")
            sys.exit(1)
        process_directory(args.input_dir, args.output_dir, args.target)
        return

    if not args.input:
        print("[ERR] 需要 --input 指定输入文件（或 --all 批量导出）")
        sys.exit(1)

    if not os.path.exists(args.input):
        print(f"[ERR] 文件不存在: {args.input}")
        sys.exit(1)

    ext = os.path.splitext(args.input)[1].lower()

    if args.json:
        process_json(args.input, args.output_dir)
    elif ext == ".xlsx":
        process_excel(args.input, args.output_dir, args.target)
    elif ext == ".json":
        process_json(args.input, args.output_dir)
    else:
        print(f"[ERR] 不支持的文件格式: {ext}")
        sys.exit(1)


if __name__ == "__main__":
    main()
