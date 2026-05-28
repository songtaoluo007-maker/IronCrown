// ============================================================================
// Infrastructure/Config/NewtonsoftConfigRepository.cs — Newtonsoft 配置加载
// 替代 Core/ConfigLoader.cs，用 Newtonsoft.Json 支持 Dictionary + 顶层数组
// ============================================================================

using System.Collections.Generic;
using System.IO;
using IronCrown.Application;
using Newtonsoft.Json;
using UnityEngine;

namespace IronCrown.Infrastructure
{
    public sealed class NewtonsoftConfigRepository : IConfigRepository
    {
        private readonly Dictionary<string, string> _cache = new();

        private static readonly JsonSerializerSettings Settings = new()
        {
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        public T Load<T>(string configName) where T : class
        {
            var json = ReadJson(configName);
            return json == null ? null : JsonConvert.DeserializeObject<T>(json, Settings);
        }

        public List<T> LoadList<T>(string configName) where T : class
        {
            var json = ReadJson(configName);
            return json == null ? new List<T>() : JsonConvert.DeserializeObject<List<T>>(json, Settings);
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        private string ReadJson(string configName)
        {
            if (_cache.TryGetValue(configName, out var cached))
                return cached;

            // TODO: Android StreamingAssets via UnityWebRequest
            var path = Path.Combine(Application.streamingAssetsPath, "Configs", "Json", configName + ".json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Config] 配置文件不存在: {path}");
                return null;
            }

            var json = File.ReadAllText(path);
            _cache[configName] = json;
            return json;
        }
    }
}
