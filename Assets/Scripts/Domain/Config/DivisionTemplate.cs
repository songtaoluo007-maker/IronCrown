// ============================================================================
// Domain/Config/DivisionTemplate.cs — 师模板配置
// C11: 师 = 多旅组合，数据驱动
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>
    /// 师模板定义。1 个师 = 多个旅组合。
    /// </summary>
    [System.Serializable]
    public class DivisionTemplate
    {
        public string id;
        public string name;
        public BrigadeEntry[] brigades;
        public int trainingTurns;
        public Dictionary<string, int> trainingCost;
        public int trainingManpowerCost;
        public int trainingEquipmentCost;
    }

    /// <summary>
    /// 旅条目（师模板内的组成）
    /// </summary>
    [System.Serializable]
    public class BrigadeEntry
    {
        public string brigadeType;  // UnitConfig.id
        public int count;
    }

    /// <summary>
    /// JSON 反序列化容器
    /// </summary>
    [System.Serializable]
    public class DivisionTemplateList
    {
        public int schemaVersion;
        public List<DivisionTemplate> items = new();
    }
}
