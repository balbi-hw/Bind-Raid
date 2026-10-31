using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChainboundRaid
{
    [Serializable]
    public sealed class RaidBossMoveDefinition
    {
        public BossAttack attack;
        public float minDistance, maxDistance = 20f;
        public float phaseOneWeight = 1f, phaseTwoWeight = 1f;
        public float cooldown = 5f, telegraph = 1.2f, recovery = 2f;
        public float turnDegreesPerSecond = 150f, maxStartAngle = 50f;
        public bool requiresChain, requiresTension, control, usesTrunk, usesLeg;
    }
    [CreateAssetMenu(menuName = "Chainbound Raid/Boss Move Set")]
    public sealed class RaidBossMoveSet : ScriptableObject
    {
        public RaidBossMoveDefinition[] moves = Array.Empty<RaidBossMoveDefinition>();
        public float idleTurnSpeed = 150f, approachSpeed = 2.5f, approachStopDistance = 6f;
        public float controlRestSeconds = 4f, brokenPartRecoveryMultiplier = 1.5f;
        public RaidBossMoveDefinition Get(BossAttack attack)
        { foreach (var move in moves) if (move.attack == attack) return move; return null; }
    }
    public struct RaidBossDecisionContext
    {
        public float distance, angle, now;
        public int phase;
        public bool targetAvailable, chainAvailable, chainTaut, controlActive;
        public float controlReadyAt;
        public BossAttack previous;
    }
    public static class RaidBossDecision
    {
        public static float Weight(RaidBossMoveDefinition move, RaidBossDecisionContext context, float readyAt)
        {
            if (!context.targetAvailable || move.attack == BossAttack.None || context.distance < move.minDistance ||
                context.distance > move.maxDistance || context.angle > move.maxStartAngle || context.now < readyAt ||
                (move.requiresChain && !context.chainAvailable) || (move.requiresTension && !context.chainTaut) ||
                (move.control && (context.controlActive || context.now < context.controlReadyAt || context.previous == move.attack))) return 0f;
            return Mathf.Max(0f, context.phase <= 1 ? move.phaseOneWeight : move.phaseTwoWeight);
        }
        public static BossAttack Select(RaidBossMoveSet settings, RaidBossDecisionContext context,
            IReadOnlyDictionary<BossAttack, float> readyTimes, float fraction)
        {
            float total = 0f;
            foreach (var move in settings.moves)
            { readyTimes.TryGetValue(move.attack, out float ready); total += Weight(move, context, ready); }
            if (total <= 0f) return BossAttack.None;
            float choice = Mathf.Clamp01(fraction) * total;
            BossAttack fallback = BossAttack.None;
            foreach (var move in settings.moves)
            {
                readyTimes.TryGetValue(move.attack, out float ready);
                float weight = Weight(move, context, ready);
                if (weight <= 0f) continue;
                fallback = move.attack; choice -= weight;
                if (choice < 0f) return move.attack;
            }
            return fallback;
        }
    }
}
