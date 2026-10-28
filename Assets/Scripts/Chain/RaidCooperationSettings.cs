using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Cooperation Settings")]
    public sealed class RaidCooperationSettings : ScriptableObject
    {
        public float minAngle = 80f;
        public float maxAngle = 160f;
        public float sharedHoldSeconds = .8f;
        public float gaugeMax = 100f;
        public float gaugeGainPerSecond = 80f;
        public float gaugeDecayPerSecond = 30f;
        public float partImmunitySeconds = 25f;
        public float bossStaggerSeconds = 3f;
    }
}
