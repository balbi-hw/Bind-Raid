using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Boss Settings")]
    public sealed class RaidBossSettings : ScriptableObject
    {
        public float stompTelegraph = 1.2f, stompRadius = 4f, stompPower = 30f;
        public float chargeTelegraph = 1.5f, chargeDistance = 8f, chargeWidth = 1.3f;
        public float chargeSpeed = 9f, chargePower = 34f;
        public float sweepTelegraph = 1f, sweepRadius = 4.5f, sweepHalfAngle = 60f;
        public float sweepPower = 22f, sweepPushDistance = 1.2f;
        public float snatchTelegraph = 1.4f, snatchResponse = 2.5f, snatchPower = 25f;
        public float snatchPullDistance = 1.5f, forcedMoveSpeedLimit = 3f;
        public float spinTelegraph = 1.8f, spinDuration = 2.5f;
        public float spinLandingPower = 28f, spinTensionPower = 20f;
        public float spinRescueSeconds = 1.5f, spinRescueRadius = 2.5f;
        public float recoverySeconds = 2f, staggerSeconds = 3f;
        public float firstAttackDelay = 3f;
    }
}
