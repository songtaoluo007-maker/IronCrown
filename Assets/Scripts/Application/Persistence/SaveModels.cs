// ============================================================================
// Application/Persistence/SaveModels.cs — 存档 DTO
// 从 Core/SaveSystem.cs 拆出，归 Application 层
// ============================================================================

using System;

namespace IronCrown.Application
{
    /// <summary>游戏存档数据结构（DTO，非运行时状态）</summary>
    [Serializable]
    public class GameState
    {
        public int turnNumber;
        public int seed;
        public ulong rngState;
        public string phase;
        public string saveTime;
        public CountrySaveData[] countries;
        public ProvinceSaveData[] provinces;
        public UnitSaveData[] units;
    }

    [Serializable]
    public class CountrySaveData
    {
        public string id;
        public int treasury;
        public int stability;
        public int warSupport;
        public int equipmentStockpile;
        public string[] activePolicies;
        public string[] completedTechs;
    }

    [Serializable]
    public class ProvinceSaveData
    {
        public string id;
        public string ownerCountry;
        public string controllerCountry;
        public int resistance;
        public int compliance;
    }

    [Serializable]
    public class UnitSaveData
    {
        public string id;
        public string unitType;
        public string ownerCountry;
        public string currentProvince;
        public int manpower;
        public int equipment;
        public int organization;
    }
}
