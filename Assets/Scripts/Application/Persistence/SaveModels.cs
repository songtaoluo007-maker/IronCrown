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
        public string selectedUnitId;
        public CountrySaveData[] countries;
        public ProvinceSaveData[] provinces;
        public UnitSaveData[] units;
        public ActiveBattleSaveData[] activeBattles;
        public WarRelationSaveData[] warRelations;
        public string gameOverResult;
        public string gameOverWinnerCountryId;
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

        // C5: 战争疲惫
        public int warExhaustion;

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
        public UnitProductionOrderSaveData[] unitProductionQueue;

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

        // 地图
        public int gridX;
        public int gridY;
        public string terrain;
        public string[] neighbors;
    }

    [Serializable]
    public class UnitProductionOrderSaveData
    {
        public string unitType;
        public int turnsRemaining;
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
        public int maxManpower;
        public int maxEquipment;
        public int maxOrganization;
        public int morale;
        public int experience;
        public int baseAttack;
        public int baseDefense;
        public int baseBreakthrough;
        public int armor;
        public int piercing;
        public int speed;
        public int movesLeft;
        public int supplyConsumption;
    }

    [Serializable]
    public class ActiveBattleSaveData
    {
        public string id;
        public string attackerUnitId;
        public string defenderUnitId;
        public string provinceId;
        public int turnsElapsed;
    }

    [Serializable]
    public class WarRelationSaveData
    {
        public string countryA;
        public string countryB;
        public int startTurn;
    }
}
