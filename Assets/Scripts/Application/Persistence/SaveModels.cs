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
        public string playerCountryId;
        public CountrySaveData[] countries;
        public ProvinceSaveData[] provinces;
        public UnitSaveData[] units;
    }

    [Serializable]
    public class CountrySaveData
    {
        public string id;
        public string name;
        public int treasury;
        public int stability;
        public int warSupport;
        public int equipmentStockpile;
        public int taxLevel;
        public int civilLevel;

        // 工厂
        public int civilianFactories;
        public int militaryFactories;
        public int dockyards;

        // 人力
        public int manpower;
        public int totalManpower;

        // 资源库存
        public ResourceEntry[] resources;
        public ConstructionOrderSaveData[] constructionQueue;

        public string[] activePolicies;
        public string[] completedTechs;
    }

    [Serializable]
    public struct ResourceEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    public class ConstructionOrderSaveData
    {
        public string factoryKind;
        public int turnsRemaining;
    }

    [Serializable]
    public class ProvinceSaveData
    {
        public string id;
        public string name;
        public string ownerCountry;
        public string controllerCountry;
        public int population;
        public int manpower;

        // 基础设施
        public int infrastructure;
        public int railwayLevel;
        public int portLevel;
        public int airBaseLevel;

        // 工业
        public int industrySlots;
        public int builtCivilianFactories;
        public int builtMilitaryFactories;

        // 资源
        public string[] resourceOutput;

        // 占领
        public int resistance;
        public int compliance;

        // 战略
        public int victoryPoint;
        public bool isCapital;
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
