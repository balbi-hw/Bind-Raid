using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    // Transport and reconciliation adapter. The shared motor contains the movement rules.
    public sealed partial class RaidPlayer
    {
        [Header("Movement simulation")]
        public RaidMovementSettings movementSettings;
        readonly NetworkVariable<RaidMovementSnapshot> movementSnapshot = new NetworkVariable<RaidMovementSnapshot>();
        readonly List<RaidMoveInput> unacknowledgedMoves = new List<RaidMoveInput>();
        RaidMotorState motorState;
        RaidMoveInput capturedMove;
        RaidMoveInput serverMove;
        RaidMovementSnapshot remoteSnapshot;
        bool ownsRuntimeSettings;
        bool snapshotReady;
        bool dodgeQueued;
        int nextMoveSequence;
        int lastReceivedMoveSequence;
        int lastSimulatedMoveSequence;
        uint simulationTick;
        uint movementRevision;
        float lastInputReceivedAt;
        Vector3 correctionOffset;
        bool wasLocalPlaying;
        const int MaxPendingMoves = 128;

        public Vector3 PresentationOffset => correctionOffset;
        public RaidAction PresentationAction => IsOwner && !IsServer && snapshotReady ? motorState.Action : Action.Value;
        bool IsPlaying => RaidSession.Instance != null &&
            (RaidSession.Instance.CurrentState == RaidSessionState.Tutorial ||
             RaidSession.Instance.CurrentState == RaidSessionState.Combat);

        void InitializeMovement()
        {
            // Preserve tuning in existing prefabs until a shared settings asset is assigned.
            if (movementSettings == null)
            {
                movementSettings = ScriptableObject.CreateInstance<RaidMovementSettings>();
                ownsRuntimeSettings = true;
                movementSettings.walkSpeed = walkSpeed;
                movementSettings.sprintSpeed = sprintSpeed;
                movementSettings.dodgeSpeed = dodgeSpeed;
                movementSettings.gravity = gravity;
                movementSettings.maxStamina = maxStamina;
                movementSettings.staminaRecoveryPerSecond = staminaRecoveryPerSecond;
                movementSettings.sprintCostPerSecond = sprintCostPerSecond;
                movementSettings.dodgeCost = dodgeCost;
                movementSettings.bracedSpeedMultiplier = bracedSpeedMultiplier;
            }
            maxStamina = movementSettings.maxStamina;
        }

        void InitializeMovementNetwork()
        {
            if (IsServer)
            {
                motorState.Stamina = Stamina.Value;
                PublishMovement();
            }
            else
            {
                if (!IsOwner) controller.enabled = false;
                ReceiveMovement(default, movementSnapshot.Value);
                movementSnapshot.OnValueChanged += ReceiveMovement;
            }
        }

        public override void OnNetworkDespawn()
        {
            RaidPlayerRegistry.Remove(this);
            movementSnapshot.OnValueChanged -= ReceiveMovement;
            unacknowledgedMoves.Clear();
            capturedMove = default;
            snapshotReady = false;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (ownsRuntimeSettings && movementSettings != null) Destroy(movementSettings);
            base.OnDestroy();
        }

        void CaptureMove(Vector3 direction, bool sprinting, bool roll)
        {
            capturedMove.Direction = direction;
            capturedMove.Sprint = sprinting;
            capturedMove.Dodge |= roll;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) ReleaseLocalInput();
        }

        void ReleaseLocalInput()
        {
            capturedMove = default;
            if (!IsSpawned || !IsOwner) return;
            lastSentPullHeld = lastSentStruggleHeld = lastSentReviveHeld = false;
            SetPullIntentRpc(false, NetworkManager.ServerTime.Time);
            SetStruggleIntentRpc(false);
            SetReviveIntentRpc(false);
            ClearAttackBufferRpc();
        }

        void FixedUpdate()
        {
            if (!IsSpawned) return;
            float dt = Time.fixedDeltaTime;
            bool playing = IsPlaying;
            if (IsOwner)
            {
                if (!playing && wasLocalPlaying) ReleaseLocalInput();
                wasLocalPlaying = playing;
                if (playing)
                {
                    var command = capturedMove;
                    command.Sequence = ++nextMoveSequence;
                    capturedMove.Dodge = false;
                    SubmitMoveRpc(command.Sequence, command.Direction, command.Sprint, command.Dodge);
                    if (!IsServer && snapshotReady)
                    {
                        // Never accumulate an unlimited replay backlog during a stalled connection.
                        if (unacknowledgedMoves.Count < MaxPendingMoves)
                        {
                            unacknowledgedMoves.Add(command);
                            SimulateMovement(command, dt, remoteSnapshot.Braced);
                        }
                    }
                }
            }
            if (!IsServer) return;
            simulationTick++;
            if (!playing)
            {
                serverMove = default;
                dodgeQueued = false;
                forcedMoveRemaining = 0f;
                CancelAttackOnServer();
                return;
            }
            if (LockEnabled.Value && LockTarget.Value.TryGet(out var lockedObject))
            {
                var focus = lockedObject.GetComponent<RaidTargetFocus>();
                if (focus != null && !focus.IsSelectable((int)LockedPart.Value)) LockedPart.Value = BossPart.Body;
                if ((lockedObject.transform.position - transform.position).sqrMagnitude > 18f * 18f) LockEnabled.Value = false;
            }
            TickStance();
            var input = Time.time - lastInputReceivedAt <= movementSettings.inputTimeoutSeconds ? serverMove : default;
            input.Dodge = dodgeQueued;
            dodgeQueued = false;
            lastSimulatedMoveSequence = lastReceivedMoveSequence;
            TickAttackBeforeMove(ref input, dt);
            motorState.Action = Action.Value;
            motorState.Stamina = Stamina.Value;
            SimulateMovement(input, dt, Stance.Value == ChainStance.Braced);
            Action.Value = motorState.Action;
            Stamina.Value = motorState.Stamina;
            TickAttackHits();
            if (Time.time - lastInputReceivedAt > movementSettings.inputTimeoutSeconds)
            {
                // Test holds are explicit server-side inputs, not a stale client connection.
                if (!testReviveHold) RescueHeld.Value = false;
                CancelPullOnServer();
                StruggleHeld.Value = false;
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitMoveRpc(int sequence, Vector3 move, bool running, bool roll)
        {
            if (sequence <= lastReceivedMoveSequence) return;
            if (float.IsNaN(move.x) || float.IsNaN(move.y) || float.IsNaN(move.z) ||
                float.IsInfinity(move.x) || float.IsInfinity(move.y) || float.IsInfinity(move.z)) return;
            lastReceivedMoveSequence = sequence;
            if (!IsPlaying) { serverMove = default; dodgeQueued = false; return; }
            move.y = 0f;
            serverMove = new RaidMoveInput { Sequence = sequence, Direction = Vector3.ClampMagnitude(move, 1f), Sprint = running };
            // A one-shot input survives newer movement messages, but runs at most once per step.
            dodgeQueued |= roll;
            lastInputReceivedAt = Time.time;
        }

        void SimulateMovement(RaidMoveInput input, float dt, bool braced)
        {
            var attack = PresentedAttack;
            bool attacking = attack != null && (motorState.Action == RaidAction.Attack || motorState.Action == RaidAction.StrongAttack);
            if (attacking && input.Dodge && NetworkManager.ServerTime.Time - AttackPlayback.Value.StartedAt >= attack.dodgeCancelOpen &&
                controller.isGrounded && motorState.Stamina >= movementSettings.dodgeCost)
            {
                // Local prediction follows the same cancellation window; the server confirms it.
                motorState.Action = RaidAction.Free;
                motorState.ActionRemaining = 0f;
                attacking = false;
            }
            if (attacking) { input.Direction *= attack.movementMultiplier; input.Sprint = false; input.Dodge = false; }
            Vector3 displacement = RaidMovementMotor.Step(ref motorState, input, movementSettings,
                controller.isGrounded, braced, transform.forward, dt);
            Vector3 horizontal = displacement;
            horizontal.y = 0f;
            if (RaidChain.Instance != null)
                horizontal = RaidChain.Instance.ConstrainMove(this, horizontal);
            if (horizontal.sqrMagnitude > .000001f)
            {
                if (attacking)
                {
                    Vector3 heading = Vector3.RotateTowards(AttackPlayback.Value.Heading, horizontal.normalized,
                        attack.maxTurnDegrees * Mathf.Deg2Rad, 0f);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading),
                        attack.turnDegreesPerSecond * dt);
                }
                else transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horizontal),
                    1f - Mathf.Exp(-movementSettings.turnSpeed * dt));
            }
            if (forcedMoveRemaining > 0f && !IsDown)
            {
                float forcedStep = Mathf.Min(forcedMoveRemaining, forcedMoveSpeed * dt);
                horizontal = forcedMoveDirection * forcedStep;
                forcedMoveRemaining -= forcedStep;
            }
            else if (RaidChain.Instance != null) horizontal += RaidChain.Instance.PullDisplacement(this, dt);
            controller.Move(horizontal + Vector3.up * displacement.y);
        }

        void TickStance()
        {
            if (!StanceChanging.Value) return;
            if (Action.Value != RaidAction.Free) StanceChanging.Value = false;
            else if (Time.time >= stanceChangeAt)
            {
                Stance.Value = pendingStance;
                StanceChanging.Value = false;
                Debug.Log("COOP_EVENT: StanceChanged player=" + OwnerClientId + " stance=" + Stance.Value);
            }
        }

        void LateUpdate()
        {
            if (IsSpawned && IsServer) PublishMovement();
        }

        void PublishMovement()
        {
            movementSnapshot.Value = new RaidMovementSnapshot {
                Initialized = true, Tick = simulationTick, Revision = movementRevision, InputSequence = lastSimulatedMoveSequence,
                Position = transform.position, Rotation = transform.rotation,
                VerticalVelocity = motorState.VerticalVelocity, Stamina = Stamina.Value,
                Action = Action.Value, ActionRemaining = motorState.ActionRemaining,
                ForcedDirection = forcedMoveDirection, ForcedRemaining = forcedMoveRemaining, ForcedSpeed = forcedMoveSpeed,
                DodgeDirection = motorState.DodgeDirection, Braced = Stance.Value == ChainStance.Braced,
                Playing = IsPlaying
            };
        }

        void ReceiveMovement(RaidMovementSnapshot previous, RaidMovementSnapshot current)
        {
            if (!current.Initialized) return;
            bool reset = !snapshotReady || current.Revision != remoteSnapshot.Revision ||
                current.Playing != remoteSnapshot.Playing;
            remoteSnapshot = current;
            snapshotReady = true;
            if (!IsOwner)
            {
                if (reset) transform.SetPositionAndRotation(current.Position, current.Rotation);
                return;
            }
            Vector3 displayedPosition = transform.position + correctionOffset;
            if (reset) unacknowledgedMoves.Clear();
            else unacknowledgedMoves.RemoveAll(command => command.Sequence <= current.InputSequence);
            controller.enabled = false;
            transform.SetPositionAndRotation(current.Position, current.Rotation);
            controller.enabled = true;
            motorState = current.Motor;
            forcedMoveDirection = current.ForcedDirection;
            forcedMoveRemaining = current.ForcedRemaining;
            forcedMoveSpeed = current.ForcedSpeed;
            // Re-evaluate collision against current geometry; the server remains authoritative
            // for moving actors, chain constraints and forced movement.
            foreach (var command in unacknowledgedMoves)
                SimulateMovement(command, Time.fixedDeltaTime, current.Braced);
            correctionOffset = reset || Vector3.Distance(displayedPosition, transform.position) > movementSettings.snapDistance ?
                Vector3.zero : displayedPosition - transform.position;
        }

        void UpdateMovementPresentation()
        {
            if (!IsSpawned || IsServer || !snapshotReady) return;
            correctionOffset = Vector3.Lerp(correctionOffset, Vector3.zero,
                1f - Mathf.Exp(-movementSettings.correctionSharpness * Time.deltaTime));
            if (IsOwner) return;
            float blend = 1f - Mathf.Exp(-movementSettings.correctionSharpness * Time.deltaTime);
            if (Vector3.Distance(transform.position, remoteSnapshot.Position) > movementSettings.snapDistance)
                transform.SetPositionAndRotation(remoteSnapshot.Position, remoteSnapshot.Rotation);
            else
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, remoteSnapshot.Position, blend),
                    Quaternion.Slerp(transform.rotation, remoteSnapshot.Rotation, blend));
        }

        void ResetMovement()
        {
            motorState = new RaidMotorState { Stamina = Stamina.Value, Action = Action.Value };
            serverMove = default;
            dodgeQueued = false;
            movementRevision++;
        }

        public void TeleportOnServer(Vector3 position)
        {
            if (!IsServer) return;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            motorState.VerticalVelocity = 0f;
            movementRevision++;
            PublishMovement();
        }
    }
}
