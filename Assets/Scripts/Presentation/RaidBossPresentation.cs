using System;
using UnityEngine;
namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Boss Presentation")]
    public sealed class RaidBossPresentation : ScriptableObject
    {
        [Serializable] public struct Move
        {
            public BossAttack attack;
            public AnimationClip clip;
            [Range(0f, 1f)] public float contact;
            public bool instantImpact;
        }
        public AnimationClip idle, walk, telegraph, stagger;
        public float transitionSeconds = .12f;
        public Move[] moves = Array.Empty<Move>();
        public AnimationClip Sample(BossAttack attack, BossAttackStage stage, float progress, out float normalized)
        {
            normalized = progress;
            foreach (var move in moves)
            {
                if (move.attack != attack || move.clip == null) continue;
                if (stage == BossAttackStage.Telegraph) { normalized = progress * move.contact; return move.clip; }
                if (stage == BossAttackStage.Active || (stage == BossAttackStage.Recovery && move.instantImpact))
                { normalized = Mathf.Lerp(move.contact, 1f, progress); return move.clip; }
                return null;
            }
            return stage == BossAttackStage.Telegraph ? telegraph : null;
        }
    }
}
