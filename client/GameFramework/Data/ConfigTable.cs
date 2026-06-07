using System.Collections.Generic;
using System.Linq;

namespace GameFramework.Data
{
    /// <summary>
    /// 策划配置行基类，所有配置行继承此接口
    /// </summary>
    public interface IConfigRow
    {
        int Id { get; }
    }

    /// <summary>
    /// 泛型配置表基类。
    /// 提供按ID索引、遍历、TryGet等通用功能。
    /// </summary>
    /// <typeparam name="T">配置行类型</typeparam>
    public class ConfigTable<T> where T : IConfigRow
    {
        private readonly Dictionary<int, T> _dataMap = new();
        private List<T> _dataList = new();

        /// <summary>
        /// 配置行数量
        /// </summary>
        public int Count => _dataMap.Count;

        /// <summary>
        /// 获取所有配置行
        /// </summary>
        public IReadOnlyList<T> All => _dataList;

        /// <summary>
        /// 根据ID获取配置行
        /// </summary>
        public T GetById(int id)
        {
            if (_dataMap.TryGetValue(id, out var row))
                return row;

            throw new KeyNotFoundException($"Config row not found: {typeof(T).Name}[{id}]");
        }

        /// <summary>
        /// 尝试根据ID获取配置行
        /// </summary>
        public bool TryGetById(int id, out T? row)
        {
            return _dataMap.TryGetValue(id, out row);
        }

        /// <summary>
        /// 判断ID是否存在
        /// </summary>
        public bool ContainsId(int id)
        {
            return _dataMap.ContainsKey(id);
        }

        /// <summary>
        /// 遍历所有配置行
        /// </summary>
        public void ForEach(System.Action<T> action)
        {
            foreach (var row in _dataList)
            {
                action(row);
            }
        }

        /// <summary>
        /// 筛选符合条件的配置行
        /// </summary>
        public IEnumerable<T> Where(System.Func<T, bool> predicate)
        {
            return _dataList.Where(predicate);
        }

        /// <summary>
        /// 加载数据（由生成的代码或外部调用）
        /// </summary>
        protected void LoadData(IEnumerable<T> rows)
        {
            _dataMap.Clear();
            _dataList = new List<T>();

            foreach (var row in rows)
            {
                _dataMap[row.Id] = row;
                _dataList.Add(row);
            }
        }

        /// <summary>
        /// 添加一行（支持热更新）
        /// </summary>
        protected void AddRow(T row)
        {
            _dataMap[row.Id] = row;
            _dataList.Add(row);
        }

        /// <summary>
        /// 移除一行
        /// </summary>
        protected bool RemoveRow(int id)
        {
            if (_dataMap.Remove(id, out var row))
            {
                _dataList.Remove(row);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空数据
        /// </summary>
        protected void Clear()
        {
            _dataMap.Clear();
            _dataList.Clear();
        }

        /// <summary>
        /// 从二进制数据 (.cfgb) 加载数据
        /// </summary>
        public void LoadFromBinary(byte[] data)
        {
            var rows = ConfigReader.ReadRows<T>(data);
            LoadData(rows);
        }

        /// <summary>
        /// 数据校验（子类可重写）
        /// </summary>
        public virtual void Validate()
        {
            // 默认不做校验
        }
    }
}
