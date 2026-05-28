// ============================================================================
// Application/Ports/ISaveRepository.cs — 存档接口
// ============================================================================

namespace IronCrown.Application
{
    public interface ISaveRepository
    {
        bool Save(string slot, GameState state);
        GameState Load(string slot);
        bool Delete(string slot);
        string[] ListSaves();
    }
}
