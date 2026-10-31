using System.Collections.Generic;
using UnityEngine;
namespace ChainboundRaid
{
    public static class RaidPlayerRegistry
    {
        static readonly List<RaidPlayer> players = new List<RaidPlayer>();
        public static IReadOnlyList<RaidPlayer> Active => players;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => players.Clear();
        public static void Register(RaidPlayer player) { if (!players.Contains(player)) players.Add(player); }
        public static void Remove(RaidPlayer player) => players.Remove(player);
    }
}
