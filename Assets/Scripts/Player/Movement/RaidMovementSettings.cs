using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Player Movement Settings")]
    public sealed class RaidMovementSettings : ScriptableObject
    {
        public float walkSpeed = 5f;
        public float sprintSpeed = 7f;
        public float dodgeSpeed = 6.4f;
        public float dodgeDistance = 3.6f;
        public float dodgeSeconds = .75f;
        public float dodgeInvulnerableStart = .18f;
        public float dodgeInvulnerableEnd = .55f;
        public float gravity = -20f;
        public float maxStamina = 100f;
        public float staminaRecoveryPerSecond = 14f;
        public float sprintCostPerSecond = 12f;
        public float dodgeCost = 20f;
        public float bracedSpeedMultiplier = .85f;
        public float turnSpeed = 12f;
        public float inputTimeoutSeconds = .3f;
        public float correctionSharpness = 18f;
        public float snapDistance = 2f;
    }

    public struct RaidMoveInput
    {
        public int Sequence;
        public Vector3 Direction;
        public bool Sprint;
        public bool Dodge;
    }

    public struct RaidMotorState
    {
        public float VerticalVelocity;
        public float Stamina;
        public RaidAction Action;
        public float ActionRemaining;
        public Vector3 DodgeDirection;
    }

    // No input devices, network, scene names or presentation dependencies.
    public static class RaidMovementMotor
    {
        public static bool IsDodgeInvulnerable(RaidMotorState state, RaidMovementSettings settings)
        {
            if (state.Action != RaidAction.Dodge || state.ActionRemaining <= 0f) return false;
            float elapsed = settings.dodgeSeconds - state.ActionRemaining;
            return elapsed >= settings.dodgeInvulnerableStart && elapsed < settings.dodgeInvulnerableEnd;
        }

        public static float DodgeDistanceAt(RaidMovementSettings settings, float elapsed)
        {
            float duration = Mathf.Max(.01f, settings.dodgeSeconds);
            float distance = Mathf.Max(0f, settings.dodgeDistance);
            if (distance <= 0f) return 0f;
            float average = distance / duration;
            float peak = Mathf.Max(average, Mathf.Min(Mathf.Max(0f, settings.dodgeSpeed), average * 2f));
            float ramp = Mathf.Clamp(1f - average / peak, 0f, .5f);
            float t = Mathf.Clamp01(elapsed / duration);
            if (ramp < .0001f) return distance * t;
            float area = t < ramp ? t * t / (2f * ramp) :
                t <= 1f - ramp ? t - ramp * .5f :
                1f - ramp - (1f - t) * (1f - t) / (2f * ramp);
            return peak * duration * area;
        }

        public static bool CanMove(RaidAction action) => action != RaidAction.Incapacitated &&
            action != RaidAction.Grabbed && action != RaidAction.Knockdown && action != RaidAction.HitReaction;

        public static Vector3 Step(ref RaidMotorState state, RaidMoveInput input,
            RaidMovementSettings settings, bool grounded, bool braced, Vector3 forward, float dt)
        {
            if (dt <= 0f) return Vector3.zero;
            bool continuingDodge = state.Action == RaidAction.Dodge && state.ActionRemaining > 0f;
            if (state.ActionRemaining > 0f && !continuingDodge)
                state.ActionRemaining = Mathf.Max(0f, state.ActionRemaining - dt);
            if (state.ActionRemaining <= 0f && (state.Action == RaidAction.Dodge ||
                state.Action == RaidAction.Attack || state.Action == RaidAction.StrongAttack ||
                state.Action == RaidAction.HitReaction || state.Action == RaidAction.Knockdown))
                state.Action = RaidAction.Free;
            if (!CanMove(state.Action)) return Vector3.zero;

            Vector3 direction = input.Direction;
            direction.y = 0f;
            direction = Vector3.ClampMagnitude(direction, 1f);
            if (input.Dodge && grounded && state.Action == RaidAction.Free && state.Stamina >= settings.dodgeCost)
            {
                state.Stamina -= settings.dodgeCost;
                state.Action = RaidAction.Dodge;
                state.ActionRemaining = settings.dodgeSeconds;
                state.DodgeDirection = direction.sqrMagnitude > .01f ? direction : forward;
            }
            bool dodging = state.Action == RaidAction.Dodge;
            bool sprinting = !dodging && input.Sprint && direction.sqrMagnitude > .01f && state.Stamina > 0f;
            state.Stamina = Mathf.Clamp(state.Stamina + (sprinting ? -settings.sprintCostPerSecond :
                settings.staminaRecoveryPerSecond) * dt, 0f, settings.maxStamina);
            float speed = (sprinting ? settings.sprintSpeed : settings.walkSpeed) *
                (braced ? settings.bracedSpeedMultiplier : 1f);
            Vector3 horizontal = direction * speed * dt;
            if (dodging)
            {
                float elapsed = settings.dodgeSeconds - state.ActionRemaining;
                horizontal = state.DodgeDirection *
                    (DodgeDistanceAt(settings, elapsed + dt) - DodgeDistanceAt(settings, elapsed));
                state.ActionRemaining = Mathf.Max(0f, state.ActionRemaining - dt);
                if (state.ActionRemaining <= 0f) state.Action = RaidAction.Free;
            }
            state.VerticalVelocity = grounded ? -1f : state.VerticalVelocity + settings.gravity * dt;
            return horizontal + Vector3.up * state.VerticalVelocity * dt;
        }
    }
}
