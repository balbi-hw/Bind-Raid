using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Player Movement Settings")]
    public sealed class RaidMovementSettings : ScriptableObject
    {
        public float walkSpeed = 5f;
        public float sprintSpeed = 7f;
        public float dodgeSpeed = 9f;
        public float dodgeSeconds = .3f;
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
        public static bool CanMove(RaidAction action) => action != RaidAction.Incapacitated &&
            action != RaidAction.Grabbed && action != RaidAction.Knockdown && action != RaidAction.HitReaction;

        public static Vector3 Step(ref RaidMotorState state, RaidMoveInput input,
            RaidMovementSettings settings, bool grounded, bool braced, Vector3 forward, float dt)
        {
            if (dt <= 0f) return Vector3.zero;
            if (state.ActionRemaining > 0f)
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
            Vector3 velocity = dodging ? state.DodgeDirection * settings.dodgeSpeed : direction * speed;
            state.VerticalVelocity = grounded ? -1f : state.VerticalVelocity + settings.gravity * dt;
            return (velocity + Vector3.up * state.VerticalVelocity) * dt;
        }
    }
}
