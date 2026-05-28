// ============================================================================
// Infrastructure/Persistence/FileSaveRepository.cs — 本地文件存档
// 替代 Core/SaveSystem.cs，用 Newtonsoft.Json 序列化
// ============================================================================

using System;
using System.IO;
using IronCrown.Application;
using Newtonsoft.Json;
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
                var state = JsonConvert.DeserializeObject<GameState>(json, Settings);
                Debug.Log($"[Save] 读档成功: {path}");
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
