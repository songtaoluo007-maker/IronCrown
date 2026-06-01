// ============================================================================
// Application/Config/ConfigRegistry.cs — 配置注册表实现
// ============================================================================

using System.Collections.Generic;
using IronCrown.Application;
using IronCrown.Domain;

namespace IronCrown.Application
{
    /// <summary>配置注册表 — 加载并缓存所有配置表</summary>
    public sealed class ConfigRegistry : IConfigRegistry
    {
        private readonly IConfigRepository _repo;

        private readonly Dictionary<string, Dictionary<string, object>> _byType = new();
        private readonly Dictionary<string, List<object>> _lists = new();

        public ConfigRegistry(IConfigRepository repo)
        {
            _repo = repo;
        }

        /// <summary>加载全部配置（启动时调用一次）</summary>
        public void LoadAll()
        {
            LoadTable<ResourceConfig>("resources");
            LoadTable<UnitConfig>("units");
            LoadTable<CountryConfig>("countries");
            LoadTable<ProvinceConfig>("provinces");
            LoadTable<EconomyConfig>("economy");
            LoadTable<DivisionTemplate>("divisionTemplates");
            LoadTable<CommanderConfig>("commanders");
        }

        public T Get<T>(string id) where T : class
        {
            var key = typeof(T).FullName;
            if (_byType.TryGetValue(key, out var dict) && dict.TryGetValue(id, out var obj))
                return obj as T;
            return null;
        }

        public IReadOnlyList<T> All<T>() where T : class
        {
            var key = typeof(T).FullName;
            if (_lists.TryGetValue(key, out var list))
                return list.ConvertAll(o => (T)o);
            return new List<T>();
        }

        public bool Has<T>(string id) where T : class
        {
            var key = typeof(T).FullName;
            return _byType.TryGetValue(key, out var dict) && dict.ContainsKey(id);
        }

        private void LoadTable<T>(string configName) where T : class
        {
            var items = _repo.LoadList<T>(configName);
            var key = typeof(T).FullName;
            var dict = new Dictionary<string, object>();
            var list = new List<object>();

            foreach (var item in items)
            {
                // 通过反射获取 id 字段
                var idField = typeof(T).GetField("id");
                var id = idField?.GetValue(item) as string;
                if (!string.IsNullOrEmpty(id))
                    dict[id] = item;
                list.Add(item);
            }

            _byType[key] = dict;
            _lists[key] = list;
        }
    }
}
