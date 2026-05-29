// ============================================================================
// Contracts/Commands/CommandResult.cs — 命令执行结果
// ============================================================================

namespace IronCrown.Contracts
{
    public sealed class CommandResult
    {
        public bool accepted;
        public string reason;

        public static CommandResult Accept() => new() { accepted = true };
        public static CommandResult Reject(string reason) => new() { accepted = false, reason = reason };
    }
}
