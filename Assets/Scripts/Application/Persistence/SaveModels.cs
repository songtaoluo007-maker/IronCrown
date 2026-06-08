// ============================================================================
// Application/Persistence/SaveModels.cs — 存档 DTO
// 从 Core/SaveSystem.cs 拆出，归 Application 层
// ============================================================================

using System;
using System.Collections.Generic;

namespace IronCrown.Application
{
    /// <summary>存档 schema 版本常量</summary>
    public static class SaveSchema
    {
        /// <summary>当前 schema 版本，每次存档结构变更时 +1</summary>
        public const int CURRENT = 2;
    }

    /// <summary>游戏存档数据结构（DTO，非运行时状态）</summary>
    [Serializable]
    public class TruceEntry { public string key; public int untilTurn; }

    [Serializable]
    public class GameState
    {
        /// <summary>存档 schema 版本，缺失时默认 1（P2.0b 迁移框架）</summary>
        public int schemaVersion;

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
        public CommanderSaveData[] commanders; // C15a: 将领
        public TileSaveData[] tiles; // P2.2: 格
        public ActiveBattleSaveData[] activeBattles;
        public WarRelationSaveData[] warRelations;
        public TruceEntry[] truces;
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

        // C7: AI 求和
        public int peaceOfferCooldown;
        public string pendingPeaceOfferFrom;
        public int pendingPeaceOfferExpiry;

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

        // C16: 抽卡系统
        public int gachaTickets;
        public int gachaPityCounter;
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

        // P2.2: 格聚合
        public string[] tileIds;
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
        public string divisionTemplateId;  // C11
        public string ownerCountry;
        public string commanderId;          // C15a: 指挥将领
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
        public BrigadeSaveData[] brigades;  // C11
        public int tacticalExp;            // C13
        public int recoveryTurnsLeft;      // C13
        public bool isCutoff;              // C13 (C14 激活)
        public int cutoffTurns;              // C14
        public bool isDisorganized;          // C14
        public bool isEntrenched;            // C9c
        public int entrenchmentBonus;        // C9c
    }

    [Serializable]
    public class BrigadeSaveData
    {
        public string brigadeType;
        public int count;
        public int manpower;
        public int equipment;
    }

    [Serializable]
    public class ActiveBattleSaveData
    {
        public string id;
        public List<string> attackerUnitIds;
        public List<string> defenderUnitIds;
        public string provinceId;
        public string attackerOwnerCountry;
        public string defenderOwnerCountry;
        public int turnsElapsed;
    }

    [Serializable]
    public class WarRelationSaveData
    {
        public string countryA;
        public string countryB;
        public int startTurn;
    }

    [Serializable]
    public class CommanderSaveData
    {
        public string id;
        public string name;
        public string ownerCountry;
        public string generalCardId;
        public int rank;
        public int victories;
        public int encirclements;
        public int baseAttack;
        public int baseDefense;
        public int maxDivisions;
        public int starLevel;
        public bool isActive;
    }

    [Serializable]
    public class TileSaveData
    {
        public string id;
        public int gridX;
        public int gridY;
        public string terrain;
        public string provinceId;
    }
}
