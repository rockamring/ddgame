using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace GameFramework.Data
{
    /// <summary>
    /// 标记配置行字段在二进制数据中的序号（对应 Excel 列序）。
    /// Python 导出器和 C# 读取器按此序号同步字段顺序。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class FieldIndexAttribute : Attribute
    {
        public int Index { get; }
        public FieldIndexAttribute(int index) => Index = index;
    }

    /// <summary>
    /// 二进制配置读取器 (.cfgb 格式)。
    /// 格式: [4B MAGIC "CFGB"] [4B ROW_COUNT] [data: fields in FieldIndex order]
    /// </summary>
    public static class ConfigReader
    {
        private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("CFGB");

        private static readonly Dictionary<Type, PropertyInfo[]> _propertyCache = new();

        /// <summary>
        /// 从二进制数据读取配置行列表
        /// </summary>
        public static List<T> ReadRows<T>(byte[] data) where T : IConfigRow
        {
            var properties = GetFieldProperties<T>();
            if (properties.Length == 0)
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} has no properties with [FieldIndex] attribute");

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            // Magic
            var magic = reader.ReadBytes(4);
            if (magic[0] != MAGIC[0] || magic[1] != MAGIC[1] ||
                magic[2] != MAGIC[2] || magic[3] != MAGIC[3])
                throw new InvalidDataException(
                    $"Invalid config binary magic: expected 'CFGB', got " +
                    $"{Encoding.ASCII.GetString(magic)}");

            int rowCount = reader.ReadInt32();
            var rows = new List<T>(rowCount);

            for (int ri = 0; ri < rowCount; ri++)
            {
                var row = (T)Activator.CreateInstance(typeof(T))!;
                foreach (var prop in properties)
                    prop.SetValue(row, ReadValue(reader, prop.PropertyType));
                rows.Add(row);
            }

            return rows;
        }

        private static PropertyInfo[] GetFieldProperties<T>()
        {
            var type = typeof(T);
            if (_propertyCache.TryGetValue(type, out var cached))
                return cached;

            var result = new List<PropertyInfo>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<FieldIndexAttribute>();
                if (attr == null || !prop.CanWrite) continue;
                result.Add(prop);
            }
            result.Sort((a, b) =>
                a.GetCustomAttribute<FieldIndexAttribute>().Index
                    .CompareTo(b.GetCustomAttribute<FieldIndexAttribute>().Index));

            var array = result.ToArray();
            _propertyCache[type] = array;
            return array;
        }

        private static object ReadValue(BinaryReader reader, Type type)
        {
            if (type == typeof(int))    return reader.ReadInt32();
            if (type == typeof(long))   return reader.ReadInt64();
            if (type == typeof(float))  return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(bool))   return reader.ReadByte() != 0;

            if (type == typeof(string))
            {
                int len = reader.ReadInt32();
                if (len <= 0) return string.Empty;
                return Encoding.UTF8.GetString(reader.ReadBytes(len));
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = type.GetGenericArguments()[0];
                int count = reader.ReadInt32();
                var list = (IList)Activator.CreateInstance(type);
                for (int i = 0; i < count; i++)
                    list.Add(ReadValue(reader, elemType));
                return list;
            }

            throw new InvalidDataException($"Unsupported field type: {type.FullName}");
        }
    }
}
