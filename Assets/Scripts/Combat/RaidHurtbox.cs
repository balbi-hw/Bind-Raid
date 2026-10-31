using UnityEngine;

namespace ChainboundRaid
{
    [RequireComponent(typeof(Collider))]
    public sealed class RaidHurtbox : MonoBehaviour
    {
        public MonoBehaviour receiver;
        public int part;
        public IRaidDamageReceiver Receiver => receiver as IRaidDamageReceiver;
    }
}
