// ============================================================================
// Contracts/Commands/CommandType.cs — 命令类型枚举
// ============================================================================

namespace IronCrown.Contracts
{
    public enum CommandType
    {
        BuildCivilianFactory,
        BuildMilitaryFactory,
        SetTaxLevel,
        SetCivilLevel,
        BuildUnit,
        MoveUnit,
        OfferPeace,
        AcceptPeace,
        RejectPeace,
        RecruitCommander,    // C15a: 招募将领
        AssignCommander,    // C15a: 分配将领到师
        UnassignCommander,   // C15a: 解除将领指挥
        DrawCard             // C16: 抽卡
    }
}
