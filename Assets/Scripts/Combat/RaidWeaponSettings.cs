using System;
using UnityEngine;

namespace ChainboundRaid
{
    [Serializable]
    public sealed class RaidAttackDefinition
    {
        public string id;
        [Min(.01f)] public float windup = .18f;
        [Min(.01f)] public float active = .12f;
        [Min(.01f)] public float recovery = .25f;
        public float basePower = 30f;
        public float partPower = 20f;
        public float staminaCost;
        public Vector3 capsuleStart = new Vector3(0f, 0f, .5f);
        public Vector3 capsuleEnd = new Vector3(0f, 0f, 2.1f);
        [Min(.01f)] public float radius = .5f;
        [Range(0f, 1f)] public float movementMultiplier = .35f;
        [Min(0f)] public float turnDegreesPerSecond = 100f;
        [Range(0f, 180f)] public float maxTurnDegrees = 40f;
        public float bufferOpen = .35f;
        public float bufferLifetime = .25f;
        public float dodgeCancelOpen = .45f;
        public int nextWeakAttack;
        public bool strong;
        public float Duration => windup + active + recovery;
        public bool IsActive(double elapsed) => elapsed >= windup && elapsed < windup + active;
        public bool CanBuffer(double elapsed) => elapsed >= bufferOpen && elapsed < Duration;
    }

    [CreateAssetMenu(menuName = "Chainbound Raid/Weapon Settings")]
    public sealed class RaidWeaponSettings : ScriptableObject
    {
        public int weakAttack;
        public int strongAttack = 1;
        public LayerMask targetLayers = ~0;
        public LayerMask obstructionLayers = ~0;
        public RaidAttackDefinition[] attacks = Array.Empty<RaidAttackDefinition>();
        public RaidAttackDefinition Get(int index) => attacks != null && index >= 0 && index < attacks.Length ? attacks[index] : null;
    }
}
