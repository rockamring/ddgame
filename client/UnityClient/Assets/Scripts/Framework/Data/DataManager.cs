using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameFramework.Core.GameSystem;

namespace GameFramework.Data
{
    /// <summary>
    /// 数据管理器，统一策划配置表的加载与访问。
    ///
    /// 对上层透明：首次访问时自动加载，使用者只需按 ID 或遍历获取数据。
    ///
    /// 使用方式：
    ///   DataManager.Get&lt;ItemConfigRow&gt;(1001);
    ///   DataManager.TryGet&lt;ItemConfigRow&gt;(1001, out var row);
    ///   foreach (var row in DataManager.All&lt;ItemConfigRow&gt;()) { ... }
    ///   DataManager.Contains&lt;ItemConfigRow&gt;(1001);
    /// </summary>
    public class DataManager : GameModule
    {
        private static DataManager? _instance;
        private static string _configDirectory = "config";
        private readonly Dictionary<Type, object> _tables = new();

        /// <summary>
        /// 配置文件目录（相对路径或绝对路径，默认 "config"）
        /// </summary>
        public static string ConfigDirectory
        {
            get => _configDirectory;
            set
            {
                _configDirectory = value;
                ConfigBytesProvider = new FileConfigBytesProvider(_configDirectory);
            }
        }

        public static IConfigBytesProvider ConfigBytesProvider { get; set; } =
            new FileConfigBytesProvider(_configDirectory);

        public override string ModuleName => "DataManager";

        // ============================================================
        // 初始化
        // ============================================================

        protected override void OnInit()
        {
            _instance = this;
        }

        protected override void OnShutdown()
        {
            _tables.Clear();
            _instance = null;
        }

        // ============================================================
        // 公开访问接口
        // ============================================================

        /// <summary>按 ID 获取配置行（自动加载）</summary>
        public static T Get<T>(int id) where T : class, IConfigRow
            => GetOrLoadTable<T>().GetById(id);

        /// <summary>尝试按 ID 获取配置行（自动加载）</summary>
        public static bool TryGet<T>(int id, out T? row) where T : class, IConfigRow
            => GetOrLoadTable<T>().TryGetById(id, out row);

        /// <summary>判断 ID 是否存在（自动加载）</summary>
        public static bool Contains<T>(int id) where T : class, IConfigRow
            => GetOrLoadTable<T>().ContainsId(id);

        /// <summary>获取所有配置行（自动加载）</summary>
        public static IReadOnlyList<T> All<T>() where T : class, IConfigRow
            => GetOrLoadTable<T>().All;

        /// <summary>配置行总数（自动加载）</summary>
        public static int Count<T>() where T : class, IConfigRow
            => GetOrLoadTable<T>().Count;

        /// <summary>遍历所有配置行（自动加载）</summary>
        public static void ForEach<T>(Action<T> action) where T : class, IConfigRow
            => GetOrLoadTable<T>().ForEach(action);

        // ============================================================
        // 手动加载
        // ============================================================

        /// <summary>手动加载指定路径的二进制配置文件</summary>
        public static void Load<T>(string path) where T : class, IConfigRow
            => LoadFromBytes<T>(File.ReadAllBytes(path));

        public static async Task PreloadAsync<T>(
            CancellationToken cancellationToken = default) where T : class, IConfigRow
        {
            var fileName = GetConfigFileName<T>();
            var data = await ConfigBytesProvider.LoadAsync(fileName, cancellationToken);
            LoadFromBytes<T>(data);
        }

        /// <summary>从字节数据加载配置表</summary>
        public static void LoadFromBytes<T>(byte[] data) where T : class, IConfigRow
        {
            var table = new ConfigTable<T>();
            table.LoadFromBinary(data);
            table.Validate();
            Instance._tables[typeof(T)] = table;
        }

        /// <summary>重新加载指定配置表（清除缓存后再次加载）</summary>
        public static void Reload<T>() where T : class, IConfigRow
        {
            Instance._tables.Remove(typeof(T));
            GetOrLoadTable<T>();
        }

        /// <summary>卸载所有配置表</summary>
        public static void UnloadAll()
        {
            Instance._tables.Clear();
        }

        /// <summary>获取原始 ConfigTable（需要高级操作时使用）</summary>
        public static ConfigTable<T> GetTable<T>() where T : class, IConfigRow
            => GetOrLoadTable<T>();

        // ============================================================
        // 内部
        // ============================================================

        private static DataManager Instance
            => _instance ?? throw new InvalidOperationException("DataManager 未初始化，请确保已注册到 GameApp");

        private static ConfigTable<T> GetOrLoadTable<T>() where T : class, IConfigRow
        {
            var type = typeof(T);
            var inst = Instance;

            if (inst._tables.TryGetValue(type, out var obj))
                return (ConfigTable<T>)obj;

            // 自动加载：按命名约定查找文件
            var fileName = GetConfigFileName<T>();
            if (!ConfigBytesProvider.SupportsSynchronousLoad)
            {
                throw new NotSupportedException(
                    $"配置表 {type.Name} 未预加载，当前配置数据 Provider 不支持同步加载。" +
                    $"请先调用 DataManager.PreloadAsync<{type.Name}>()。路径: {ConfigBytesProvider.GetDisplayPath(fileName)}");
            }

            try
            {
                LoadFromBytes<T>(ConfigBytesProvider.Load(fileName));
                return (ConfigTable<T>)inst._tables[type];
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (DirectoryNotFoundException ex)
            {
                var path = ConfigBytesProvider.GetDisplayPath(fileName);
                throw new FileNotFoundException(
                    $"配置表 {type.Name} 未加载，且未找到自动加载文件: {path}",
                    path,
                    ex);
            }
        }

        private static string GetConfigFileName<T>()
        {
            var name = typeof(T).Name;
            const string prefix = "Config_";
            if (name.StartsWith(prefix))
                name = name.Substring(prefix.Length);
            return name + ".cfgb";
        }
    }
}
