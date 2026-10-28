using System;
using UnityEngine;

namespace ChainboundRaid
{
    public enum ChainStance { Flexible, Braced }

    [Flags]
    public enum PullFailure
    {
        None = 0,
        NotBraced = 1,
        NotGrounded = 2,
        InvalidAngle = 4,
        NoSharedHold = 8,
        NoAnchor = 16,
        PartImmune = 32,
        PlayerDisabled = 64
    }

    public static class RaidCooperationRules
    {
        public static float Angle(Vector3 boss, Vector3 first, Vector3 second)
        {
            Vector3 a = first - boss, b = second - boss;
            a.y = b.y = 0f;
            if (a.sqrMagnitude < .001f || b.sqrMagnitude < .001f) return 0f;
            return Vector3.Angle(a, b);
        }

        public static PullFailure Evaluate(bool firstBraced, bool secondBraced,
            bool firstGrounded, bool secondGrounded, bool firstEnabled, bool secondEnabled,
            bool firstHolding, bool secondHolding, float angle, float minAngle, float maxAngle,
            bool anchorValid, bool partImmune)
        {
            PullFailure failure = PullFailure.None;
            if (!firstBraced || !secondBraced) failure |= PullFailure.NotBraced;
            if (!firstGrounded || !secondGrounded) failure |= PullFailure.NotGrounded;
            if (!firstEnabled || !secondEnabled) failure |= PullFailure.PlayerDisabled;
            if (!firstHolding || !secondHolding) failure |= PullFailure.NoSharedHold;
            if (angle < minAngle || angle > maxAngle) failure |= PullFailure.InvalidAngle;
            if (!anchorValid) failure |= PullFailure.NoAnchor;
            if (partImmune) failure |= PullFailure.PartImmune;
            return failure;
        }

        public static PullFailure Primary(PullFailure reasons)
        {
            PullFailure[] order = { PullFailure.PlayerDisabled, PullFailure.NoAnchor,
                PullFailure.NotBraced, PullFailure.NotGrounded, PullFailure.InvalidAngle,
                PullFailure.NoSharedHold, PullFailure.PartImmune };
            foreach (var reason in order)
                if ((reasons & reason) != 0) return reason;
            return PullFailure.None;
        }
    }
}
