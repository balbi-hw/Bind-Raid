using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Character Presentation")]
    public sealed class RaidCharacterPresentation : ScriptableObject
    {
        public AnimationClip idle, walk, sprint, weakAttack, strongAttack;
        public AnimationClip dodge, hitReaction, knockdown, incapacitated, grabbed;
        public AnimationClip getUp, brace, pull, rescue;
        public float transitionSeconds = .12f;
        public float walkReferenceSpeed = 5f;
        public float sprintReferenceSpeed = 7f;
        public bool proceduralFallback = true;
        public AnimationClip ClipFor(RaidAction action, bool pulling, bool rescuing, bool braced)
        {
            switch (action)
            {
                case RaidAction.Attack: return weakAttack;
                case RaidAction.StrongAttack: return strongAttack;
                case RaidAction.Dodge: return dodge;
                case RaidAction.HitReaction: return hitReaction;
                case RaidAction.Knockdown: return knockdown;
                case RaidAction.Incapacitated: return incapacitated;
                case RaidAction.Grabbed: return grabbed;
                default: return pulling ? pull : rescuing ? rescue : braced ? brace : null;
            }
        }
    }
}
