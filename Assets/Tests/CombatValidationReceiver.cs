#if UNITY_EDITOR
using UnityEngine;
namespace ChainboundRaid.Editor
{
    public sealed class CombatValidationReceiver : MonoBehaviour, IRaidDamageReceiver
    {
        public int Hits;
        public int Part;
        public float Power;
        public bool TryReceiveHit(RaidHitRequest hit) { Hits++; Part = hit.Part; Power = hit.BasePower; return true; }
    }
}

#endif
