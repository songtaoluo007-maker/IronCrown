// ============================================================================
// Domain/State/WorldState.cs — 运行时世界根
// 从 Domain/Diplomacy.cs 拆出并扩展
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>运行时世界状态（Simulation 唯一认的世界根）</summary>
    public class WorldState
    {
        // === 原有字段 ===
        public int worldTension;
        public int turnNumber;
        public string playerCountryId;
        public string selectedUnitId;
        public Dictionary<string, int> truceUntilTurn = new(); // key="{lo}_vs_{hi}", value=truce截止回合

        // === 扩展字段 ===
        public Dictionary<string, CountryState> countries = new();
        public Dictionary<string, ProvinceState> provinces = new();
        public Dictionary<string, UnitState> units = new();
        public Dictionary<string, CommanderState> commanders = new(); // C15a: 将领
        public Dictionary<string, TileState> tiles = new(); // P2.2: 格
        public List<ActiveBattle> activeBattles = new();
        public List<WarRelation> warRelations = new();
        public List<DiplomacyRelation> relations = new();

        // === P2.5: 空间索引 (provinceId → unitIds) ===
        public Dictionary<string, List<string>> provinceUnitIds = new();

        // === 游戏终局 ===
        public string gameOverResult;           // "Victory" | "Defeat" | null
        public string gameOverWinnerCountryId;  // 玩家胜=playerCountryId；失败=null

        public void AddTension(int amount, string reason = null)
        {
            worldTension = System.Math.Min(100, worldTension + amount);
        }

        public bool TensionAllows(int threshold)
        {
            return worldTension >= threshold;
        }

        // === P2.5: 空间索引维护 ===

        /// <summary>重建省→部队索引（从 units 字典全量构建）</summary>
        public void RebuildProvinceUnitIndex()
        {
            provinceUnitIds.Clear();
            foreach (var u in units.Values)
            {
                if (string.IsNullOrEmpty(u.currentProvinceId)) continue;
                if (!provinceUnitIds.ContainsKey(u.currentProvinceId))
                    provinceUnitIds[u.currentProvinceId] = new List<string>();
                provinceUnitIds[u.currentProvinceId].Add(u.id);
            }
        }

        /// <summary>获取省驻军 ID 列表（O(1)）</summary>
        public List<string> GetUnitsInProvince(string provinceId)
        {
            return provinceUnitIds.TryGetValue(provinceId, out var list) ? list : new List<string>();
        }
    }
}
