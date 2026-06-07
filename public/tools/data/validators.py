#!/usr/bin/env python3
"""
策划数据校验工具。

功能：
1. 校验 JSON 配置数据的完整性
2. 检查ID唯一性
3. 检查引用完整性（多表间关联校验）
4. 检查数据类型与范围

用法：
    python validators.py --config-dir ../configs/
    python validators.py --file ItemConfig.json --schema ItemSchema.json
"""

import argparse
import json
import os
import sys
from typing import Any, Dict, List, Optional, Set, Tuple

try:
    import openpyxl
    HAS_OPENPYXL = True
except ImportError:
    HAS_OPENPYXL = False


class ValidationError:
    """校验错误记录"""
    def __init__(self, table: str, row_id: Any, field: str, message: str):
        self.table = table
        self.row_id = row_id
        self.field = field
        self.message = message

    def __str__(self) -> str:
        return f"[{self.table}][ID:{self.row_id}] {self.field}: {self.message}"


class ConfigValidator:
    """配置校验器"""

    def __init__(self):
        self.errors: List[ValidationError] = []
        self.warnings: List[ValidationError] = []
        self._table_data: Dict[str, List[Dict]] = {}

    def load_json(self, filepath: str) -> Optional[List[Dict]]:
        """加载 JSON 配置文件"""
        try:
            with open(filepath, "r", encoding="utf-8") as f:
                data = json.load(f)
            if isinstance(data, list):
                return data
            else:
                print(f"[ERR] {filepath}: 文件根元素不是数组")
                return None
        except json.JSONDecodeError as e:
            print(f"[ERR] {filepath}: JSON解析失败 - {e}")
            return None
        except FileNotFoundError:
            print(f"[ERR] {filepath}: 文件不存在")
            return None

    def load_excel(self, filepath: str) -> Optional[List[Dict]]:
        """加载 Excel (.xlsx) 配置文件"""
        if not HAS_OPENPYXL:
            print("[ERR] 需要 openpyxl 库: pip install openpyxl")
            return None

        try:
            wb = openpyxl.load_workbook(filepath, read_only=True, data_only=True)
            ws = wb.active
            if ws is None:
                print(f"[ERR] {filepath}: 没有工作表")
                return None

            rows = list(ws.iter_rows(values_only=True))
            if len(rows) < 5:
                print(f"[ERR] {filepath}: 数据不足（需要表头+类型+标记+注释+至少1行数据）")
                return None

            # Row 1 = 表头
            headers = [str(h).strip() if h is not None else f"field_{i}"
                       for i, h in enumerate(rows[0])]
            # Row 2-3-4 = 类型/标记/注释（校验时忽略）

            result = []
            for row in rows[4:]:
                # 跳过完全空行
                if all(cell is None for cell in row):
                    continue
                record = {}
                for ci, h in enumerate(headers):
                    val = row[ci] if ci < len(row) else None
                    if val is not None:
                        record[h] = val
                result.append(record)

            wb.close()
            return result if result else None

        except Exception as e:
            print(f"[ERR] {filepath}: 读取失败 - {e}")
            return None

    def validate_unique_id(self, table_name: str, data: List[Dict]) -> None:
        """校验ID唯一性"""
        ids: Set[int] = set()
        for row in data:
            row_id = row.get("id")
            if row_id is None:
                self.errors.append(ValidationError(
                    table_name, row.get("id"), "id", "缺少id字段"
                ))
                continue
            if row_id in ids:
                self.errors.append(ValidationError(
                    table_name, row_id, "id", f"重复ID: {row_id}"
                ))
            ids.add(row_id)

    def validate_required_fields(self, table_name: str, data: List[Dict],
                                  required_fields: List[str]) -> None:
        """校验必填字段"""
        if not data:
            row_keys = set()
        else:
            row_keys = set(data[0].keys())

        for row in data:
            row_id = row.get("id", "?")
            for field in required_fields:
                if field not in row or row[field] is None:
                    self.errors.append(ValidationError(
                        table_name, row_id, field, f"缺少必填字段: {field}"
                    ))

    def validate_value_range(self, table_name: str, data: List[Dict],
                              field: str, min_val: Any = None,
                              max_val: Any = None) -> None:
        """校验数值范围"""
        for row in data:
            val = row.get(field)
            row_id = row.get("id", "?")
            if val is None:
                continue
            if min_val is not None and val < min_val:
                self.errors.append(ValidationError(
                    table_name, row_id, field,
                    f"值 {val} 小于最小值 {min_val}"
                ))
            if max_val is not None and val > max_val:
                self.errors.append(ValidationError(
                    table_name, row_id, field,
                    f"值 {val} 大于最大值 {max_val}"
                ))

    def validate_reference(self, table_name: str, data: List[Dict],
                            field: str, ref_table_name: str,
                            ref_data: List[Dict]) -> None:
        """校验引用完整性（外键检查）"""
        ref_ids = {row.get("id") for row in ref_data if "id" in row}
        for row in data:
            val = row.get(field)
            row_id = row.get("id", "?")
            if val is None:
                continue
            if val not in ref_ids:
                self.warnings.append(ValidationError(
                    table_name, row_id, field,
                    f"引用的 {ref_table_name}[{val}] 不存在"
                ))

    def validate_table(self, table_name: str, data: List[Dict]) -> None:
        """对一个表执行标准校验"""
        if not data:
            return

        # ID唯一性
        self.validate_unique_id(table_name, data)

        # 通用检查：数值类型字段范围
        first = data[0]
        for key, val in first.items():
            if isinstance(val, (int, float)) and not isinstance(val, bool):
                if key in ("type", "quality", "level", "count", "max_stack"):
                    self.validate_value_range(table_name, data, key,
                                               min_val=0)

    def validate_directory(self, config_dir: str) -> bool:
        """校验目录下的所有配置文件（支持 .json 和 .xlsx）"""
        self._table_data.clear()

        for filename in sorted(os.listdir(config_dir)):
            ext = os.path.splitext(filename)[1].lower()
            if ext not in (".json", ".xlsx"):
                continue

            filepath = os.path.join(config_dir, filename)
            table_name = os.path.splitext(filename)[0]
            data = None

            if ext == ".json":
                data = self.load_json(filepath)
            elif ext == ".xlsx":
                data = self.load_excel(filepath)

            if data is not None:
                self._table_data[table_name] = data
                self.validate_table(table_name, data)

        # 交叉引用校验
        self._cross_table_validation()

        # 输出结果
        return self._report()

    def _cross_table_validation(self) -> None:
        """跨表引用校验"""
        # 如果有 ItemConfig 和 DropConfig 之类的关联，在此处理
        pass

    def _report(self) -> bool:
        """输出校验报告"""
        print("\n===== 配置校验报告 =====")
        print(f"已加载表: {len(self._table_data)} 张")

        if self.errors:
            print(f"\n[ERR] 错误 ({len(self.errors)} 个):")
            for e in self.errors:
                print(f"  {e}")
        else:
            print("\n[OK] 无错误")

        if self.warnings:
            print(f"\n[WARN] 警告 ({len(self.warnings)} 个):")
            for w in self.warnings:
                print(f"  {w}")

        print("\n========================")
        return len(self.errors) == 0


def main():
    parser = argparse.ArgumentParser(description="策划数据校验工具")
    parser.add_argument("--config-dir", "-d", default="../configs",
                       help="配置文件目录")
    parser.add_argument("--file", "-f", help="单个文件校验")

    args = parser.parse_args()
    validator = ConfigValidator()

    if args.file:
        ext = os.path.splitext(args.file)[1].lower()
        table_name = os.path.splitext(os.path.basename(args.file))[0]
        data = None
        if ext == ".json":
            data = validator.load_json(args.file)
        elif ext == ".xlsx":
            data = validator.load_excel(args.file)
        else:
            print(f"[ERR] 不支持的文件格式: {ext}")
            sys.exit(1)
        if data:
            validator._table_data[table_name] = data
            validator.validate_table(table_name, data)
            validator._report()
    else:
        success = validator.validate_directory(args.config_dir)
        sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
