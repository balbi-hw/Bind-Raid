using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Boss Settings")]
    public sealed class RaidBossSettings : ScriptableObject
    {
        public float stompTelegraph = 1.2f, stompRadius = 6.5f, stompPower = 30f;
        public float chargeTelegraph = 1.5f, chargeDistance = 10f, chargeWidth = 3.2f;
        public float chargeBodyFront = 4.5f, chargeBodyBack = 3.5f;
        public float chargeSpeed = 9f, chargePower = 34f;
        public float sweepTelegraph = 1f, sweepRadius = 7.5f, sweepHalfAngle = 70f;
        public float sweepPower = 22f, sweepPushDistance = 1.2f;
        public float snatchTelegraph = 1.4f, snatchResponse = 2.5f, snatchPower = 25f;
        public float snatchPullDistance = 1.5f, forcedMoveSpeedLimit = 3f;
        public float spinTelegraph = 1.8f, spinDuration = 2.5f, spinGrabRange = 7f;
        public float spinLandingPower = 28f, spinTensionPower = 20f;
        public float spinRescueSeconds = 1.5f, spinRescueRadius = 2.5f;
        public float recoverySeconds = 2f, staggerSeconds = 3f;
        public float firstAttackDelay = 3f;
    }
}
