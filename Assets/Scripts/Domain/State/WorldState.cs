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
    }
}
