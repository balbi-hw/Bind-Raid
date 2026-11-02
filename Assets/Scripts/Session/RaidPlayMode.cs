namespace ChainboundRaid
{
    public enum RaidPlayModeKind { Multiplayer, SoloDevelopment }

    public interface IRaidPlayMode
    {
        int RequiredPlayers { get; }
        bool UsesCooperativeTutorial { get; }
        bool AllowsBossAttack(BossAttack attack);
        bool IsPartyDefeated(System.Collections.Generic.IReadOnlyList<RaidPlayer> players);
    }

    public sealed class MultiplayerRaidMode : IRaidPlayMode
    {
        public int RequiredPlayers => 2;
        public bool UsesCooperativeTutorial => true;
        public bool AllowsBossAttack(BossAttack attack) => true;
        public bool IsPartyDefeated(System.Collections.Generic.IReadOnlyList<RaidPlayer> players) =>
            players.Count == 2 && players[0].IsDown && players[1].IsDown;
    }

    public sealed class SoloDevelopmentRaidMode : IRaidPlayMode
    {
        public int RequiredPlayers => 1;
        public bool UsesCooperativeTutorial => false;
        public bool AllowsBossAttack(BossAttack attack) => attack == BossAttack.Stomp ||
            attack == BossAttack.Charge || attack == BossAttack.TrunkSweep;
        public bool IsPartyDefeated(System.Collections.Generic.IReadOnlyList<RaidPlayer> players) =>
            players.Count == 1 && players[0].IsDown;
    }

    public static class RaidPlayModeFactory
    {
        public static IRaidPlayMode Create(RaidPlayModeKind kind) => kind == RaidPlayModeKind.SoloDevelopment ?
            new SoloDevelopmentRaidMode() : new MultiplayerRaidMode();
    }
}
