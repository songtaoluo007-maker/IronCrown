// ============================================================================
// Infrastructure/Persistence/FileSaveRepository.cs — 本地文件存档
// 替代 Core/SaveSystem.cs，用 Newtonsoft.Json 序列化
// ============================================================================

using System;
using System.IO;
using System.Linq;
using IronCrown.Application;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IronCrown.Infrastructure
{
    public sealed class FileSaveRepository : ISaveRepository
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
        };

        private string SaveDirectory => Path.Combine(UnityEngine.Application.persistentDataPath, "Saves");

        public bool Save(string slot, GameState state)
        {
            try
            {
                if (!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);

                var json = JsonConvert.SerializeObject(state, Settings);
                var path = Path.Combine(SaveDirectory, slot + ".json");
                File.WriteAllText(path, json);
                Debug.Log($"[Save] 存档成功: {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 存档失败: {e.Message}");
                return false;
            }
        }

        public GameState Load(string slot)
        {
            try
            {
                var path = Path.Combine(SaveDirectory, slot + ".json");
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[Save] 存档不存在: {path}");
                    return null;
                }

                var json = File.ReadAllText(path);

                // P2.0b: 迁移框架 — 先解析为 JObject，经 SaveMigrationRunner 升级后再反序列化
                var raw = JObject.Parse(json);
                var runner = new SaveMigrationRunner(new ISaveMigration[]
                {
                    new Migration_0to1()
                    // P2.2 地图迁移器在此追加
                });
                raw = runner.Upgrade(raw);

                var state = raw.ToObject<GameState>(JsonSerializer.Create(Settings));
                Debug.Log($"[Save] 读档成功: {path} (schemaVersion={state.schemaVersion})");
                return state;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 读档失败: {e.Message}");
                return null;
            }
        }

        public bool Delete(string slot)
        {
            var path = Path.Combine(SaveDirectory, slot + ".json");
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public string[] ListSaves()
        {
            if (!Directory.Exists(SaveDirectory)) return Array.Empty<string>();
            var files = Directory.GetFiles(SaveDirectory, "*.json");
            for (int i = 0; i < files.Length; i++)
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            return files;
        }
    }
}
