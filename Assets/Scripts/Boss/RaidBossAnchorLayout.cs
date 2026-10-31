using UnityEngine;
namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Boss Anchor Layout")]
    public sealed class RaidBossAnchorLayout : ScriptableObject
    {
        public Vector3 frontLeg, trunk, rescue;
    }
}
