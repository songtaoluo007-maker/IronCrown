// ============================================================================
// Domain/State/BrigadeState.cs — 旅状态（师的组成部分）
// C11: 师 = 多旅组合
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>
    /// 旅状态。1 个旅 = N 个同兵种营。
    /// brigadeType 对应 UnitConfig.id（infantry/artillery/...）
    /// </summary>
    public sealed class BrigadeState
    {
        public string brigadeType;   // UnitConfig.id
        public int count;            // 营数量
        public int manpower;         // 当前总人力
        public int equipment;        // 当前总装备

        // C12: 旅级战损
        public void TakeDamage(int orgDmg, int strDmg)
        {
            manpower  = System.Math.Max(0, manpower  - strDmg);
            equipment = System.Math.Max(0, equipment - strDmg);
        }

        /// <summary>旅是否已丧失战力（人力或装备归零）</summary>
        public bool IsDestroyed => manpower <= 0 || equipment <= 0;
    }
}
