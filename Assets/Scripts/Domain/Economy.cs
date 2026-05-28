// ============================================================================
// Domain/Economy.cs — 经济系统运行时数据模型
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>工厂类型</summary>
    public enum FactoryType
    {
        Civilian,  // 民用 — 建设/贸易/修复
        Military,  // 军用 — 生产装备
        Dockyard   // 造船 — 生产舰船
    }

    /// <summary>生产线状态</summary>
    [System.Serializable]
    public class ProductionLineState
    {
        public string id;
        public string equipmentType;     // 生产的装备类型
        public FactoryType factoryType;
        public int allocatedFactories;   // 投入工厂数
        public int progress;             // 当前进度
        public int costPerUnit;          // 每单位消耗
        public int efficiency;           // 生产效率 0-100
        public int stockpile;            // 库存数量

        /// <summary>每回合产量</summary>
        public int OutputPerTurn => allocatedFactories * efficiency / 100;
    }
}
