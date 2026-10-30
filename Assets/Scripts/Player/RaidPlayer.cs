using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public enum RaidAction { Free, Attack, StrongAttack, Dodge, HitReaction, Knockdown, Grabbed, Incapacitated }
    public enum BossPart { Body, Trunk, FrontLeg }

    public static class DamageCalculator
    {
        // Keep the formula in one place so equipment and buffs can supply attack/defense later.
        public static float Calculate(float basePower, float attack, float defense)
        {
            return Mathf.Max(1f, Mathf.Round(basePower * Mathf.Max(1f, attack) /
                (Mathf.Max(1f, attack) + Mathf.Max(0f, defense))));
        }
    }

    public static class ReviveRules
    {
        public static float RequiredSeconds(int playerDownCount) => 3f * Mathf.Max(1, playerDownCount);
    }

    [RequireComponent(typeof(CharacterController), typeof(NetworkObject))]
    public sealed class RaidPlayer : NetworkBehaviour
    {
        [Header("Tuning")]
        public float maxHealth = 100f;
        public float attackPower = 40f;
        public float defense = 10f;
        public float walkSpeed = 5f;
        public float sprintSpeed = 7f;
        public float dodgeSpeed = 9f;
        public float gravity = -20f;
        public float maxStamina = 100f;
        public float staminaRecoveryPerSecond = 14f;
        public float sprintCostPerSecond = 12f;
        public float dodgeCost = 20f;
        public float strongCost = 15f;
        public float reviveHealthFraction = 0.35f;
        public float weakBasePower = 30f;
        public float strongBasePower = 55f;
        public float weakPartPower = 20f;
        public float strongPartPower = 45f;
        public float attackRange = 2.6f;

        [Header("Keyboard and mouse")]
        public KeyCode forward = KeyCode.W, back = KeyCode.S, left = KeyCode.A, right = KeyCode.D;
        public KeyCode sprint = KeyCode.LeftShift, dodge = KeyCode.Space, weakAttack = KeyCode.Mouse0;
        public KeyCode strongAttack = KeyCode.Mouse1, lockOn = KeyCode.Tab, revive = KeyCode.E;
        public KeyCode stanceToggle = KeyCode.Q, pull = KeyCode.F;
        public KeyCode struggle = KeyCode.R;
        public float stanceTransitionSeconds = .4f;
        public float stanceCooldownSeconds = 1f;
        public float bracedSpeedMultiplier = .85f;

        public readonly NetworkVariable<float> Health = new NetworkVariable<float>();
        public readonly NetworkVariable<float> Stamina = new NetworkVariable<float>();
        public readonly NetworkVariable<int> DownCount = new NetworkVariable<int>();
        public readonly NetworkVariable<RaidAction> Action = new NetworkVariable<RaidAction>();
        public readonly NetworkVariable<float> ReviveProgress = new NetworkVariable<float>();
        public readonly NetworkVariable<BossPart> LockedPart = new NetworkVariable<BossPart>();
        public readonly NetworkVariable<bool> LockEnabled = new NetworkVariable<bool>();
        public readonly NetworkVariable<ChainStance> Stance = new NetworkVariable<ChainStance>();
        public readonly NetworkVariable<bool> StanceChanging = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> PullHeld = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> StruggleHeld = new NetworkVariable<bool>();

        CharacterController controller;
        float verticalVelocity;
        float dodgeUntil;
        float attackUntil;
        Vector3 dodgeDirection;
        float lastMoveTime;
        int lastAttackSequence;
        int localAttackSequence;
        bool reviveHeld;
        bool lastSentReviveHeld;
        bool testReviveHold;
        bool lastSentPullHeld;
        bool lastSentStruggleHeld;
        Vector3 forcedMoveDirection;
        float forcedMoveRemaining;
        float forcedMoveSpeed;
        float stanceChangeAt;
        float nextStanceAllowed;
        ChainStance pendingStance;
        public double PullHeldSince { get; private set; }
        public bool IsGrounded => controller != null && (controller.isGrounded ||
            Physics.Raycast(transform.position + Vector3.up * .1f, Vector3.down,
                1.2f, ~0, QueryTriggerInteraction.Ignore));
        public bool CanCooperate => Action.Value == RaidAction.Free && !StanceChanging.Value;

        public bool IsDown => Action.Value == RaidAction.Incapacitated;
        public float RequiredReviveSeconds => ReviveRules.RequiredSeconds(DownCount.Value);

        void Awake() { controller = GetComponent<CharacterController>(); }

        public override void OnNetworkSpawn()
        {
            Debug.Log("RAID_TEST: player spawned owner=" + OwnerClientId + " local=" + IsOwner);
            if (IsServer)
            {
                Health.Value = maxHealth; Stamina.Value = maxStamina; Action.Value = RaidAction.Free;
                Stance.Value = ChainStance.Flexible; StanceChanging.Value = false; PullHeld.Value = false;
                StruggleHeld.Value = false;
            }
            if (IsOwner)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    var follow = camera.GetComponent<PrototypeCamera>();
                    if (follow != null) follow.target = transform;
                }
            }
        }

        void Update()
        {
            if (IsServer && forcedMoveRemaining > 0f)
            {
                float step = Mathf.Min(forcedMoveRemaining, forcedMoveSpeed * Time.deltaTime);
                controller.Move(forcedMoveDirection * step);
                forcedMoveRemaining -= step;
            }
            if (IsServer && StanceChanging.Value)
            {
                if (Action.Value != RaidAction.Free) StanceChanging.Value = false;
                else if (Time.time >= stanceChangeAt)
                {
                    Stance.Value = pendingStance;
                    StanceChanging.Value = false;
                    Debug.Log("COOP_EVENT: StanceChanged player=" + OwnerClientId + " stance=" + Stance.Value);
                }
            }
            if (!IsSpawned || !IsOwner || RaidSession.Instance == null ||
                (RaidSession.Instance.CurrentState != RaidSessionState.Combat &&
                 RaidSession.Instance.CurrentState != RaidSessionState.Tutorial)) return;

            Vector2 input = new Vector2((Input.GetKey(right) ? 1 : 0) - (Input.GetKey(left) ? 1 : 0),
                (Input.GetKey(forward) ? 1 : 0) - (Input.GetKey(back) ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1f);
            var camera = Camera.main;
            Vector3 move = new Vector3(input.x, 0, input.y);
            if (camera != null)
            {
                var f = camera.transform.forward; f.y = 0; f.Normalize();
                var r = camera.transform.right; r.y = 0; r.Normalize();
                move = f * input.y + r * input.x;
            }
            move = Vector3.ClampMagnitude(move, 1f);
            SubmitMoveRpc(move, Input.GetKey(sprint), Input.GetKeyDown(dodge));

            if (Input.GetKeyDown(lockOn)) CycleLockRpc();
            if (Input.GetKeyDown(weakAttack)) AttackRpc(false, ++localAttackSequence);
            if (Input.GetKeyDown(strongAttack)) AttackRpc(true, ++localAttackSequence);
            if (Input.GetKeyDown(stanceToggle)) RequestStanceRpc();
            bool pulling = Input.GetKey(pull);
            if (pulling != lastSentPullHeld)
            {
                lastSentPullHeld = pulling;
                SetPullIntentRpc(pulling, NetworkManager.ServerTime.Time);
            }
            bool struggling = Input.GetKey(struggle);
            if (struggling != lastSentStruggleHeld)
            {
                lastSentStruggleHeld = struggling;
                SetStruggleIntentRpc(struggling);
            }
            bool held = testReviveHold || Input.GetKey(revive);
            if (held != lastSentReviveHeld) { lastSentReviveHeld = held; SetReviveIntentRpc(held); }
        }

        [Rpc(SendTo.Server)]
        void SubmitMoveRpc(Vector3 move, bool running, bool roll)
        {
            if (RaidSession.Instance == null ||
                (RaidSession.Instance.CurrentState != RaidSessionState.Combat &&
                 RaidSession.Instance.CurrentState != RaidSessionState.Tutorial) || IsDown)
            {
                if (roll) Debug.Log("RAID_ACTION_REJECTED: dodge / unavailable state");
                return;
            }
            float now = Time.time;
            float dt = Mathf.Clamp(now - lastMoveTime, 0f, 0.05f);
            lastMoveTime = now;
            if (dt <= 0) return;
            if ((Action.Value == RaidAction.HitReaction || Action.Value == RaidAction.Knockdown) && now >= attackUntil)
                Action.Value = RaidAction.Free;
            if (Action.Value == RaidAction.HitReaction || Action.Value == RaidAction.Knockdown ||
                Action.Value == RaidAction.Grabbed) return;
            move.y = 0; move = Vector3.ClampMagnitude(move, 1f);
            if (roll && Action.Value == RaidAction.Free && controller.isGrounded &&
                Stamina.Value >= dodgeCost && now >= dodgeUntil)
            {
                Stamina.Value -= dodgeCost;
                dodgeDirection = move.sqrMagnitude > 0.01f ? move : transform.forward;
                dodgeUntil = now + 0.3f;
                Action.Value = RaidAction.Dodge;
            }
            else if (roll) Debug.Log("RAID_ACTION_REJECTED: dodge / state, ground or stamina");
            bool dodging = now < dodgeUntil;
            bool sprinting = !dodging && running && move.sqrMagnitude > 0.01f && Stamina.Value > 0;
            float cost = sprinting ? sprintCostPerSecond * dt : -staminaRecoveryPerSecond * dt;
            Stamina.Value = Mathf.Clamp(Stamina.Value - cost, 0f, maxStamina);
            float stanceFactor = Stance.Value == ChainStance.Braced ? bracedSpeedMultiplier : 1f;
            Vector3 horizontal = dodging ? dodgeDirection * dodgeSpeed :
                move * (sprinting ? sprintSpeed : walkSpeed) * stanceFactor;
            if (horizontal.sqrMagnitude > 0.01f) transform.forward = Vector3.Slerp(transform.forward, horizontal.normalized, 12f * dt);
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + gravity * dt;
            Vector3 horizontalStep = horizontal * dt;
            if (RaidChain.Instance != null)
                horizontalStep = RaidChain.Instance.ConstrainMove(this, horizontalStep);
            controller.Move(horizontalStep + Vector3.up * verticalVelocity * dt);
            if (Action.Value == RaidAction.Dodge && !dodging) Action.Value = RaidAction.Free;
            if ((Action.Value == RaidAction.Attack || Action.Value == RaidAction.StrongAttack ||
                Action.Value == RaidAction.HitReaction) && now >= attackUntil)
                Action.Value = RaidAction.Free;
        }

        [Rpc(SendTo.Server)]
        void CycleLockRpc()
        {
            if (IsDown || RaidBoss.Instance == null ||
                Vector3.Distance(transform.position, RaidBoss.Instance.transform.position) > 18f) return;
            if (!LockEnabled.Value) { LockEnabled.Value = true; LockedPart.Value = BossPart.Body; }
            else if (LockedPart.Value == BossPart.FrontLeg) LockEnabled.Value = false;
            else LockedPart.Value = (BossPart)((int)LockedPart.Value + 1);
        }

        [Rpc(SendTo.Server)]
        void AttackRpc(bool strong, int sequence)
        {
            if (sequence <= lastAttackSequence) return;
            lastAttackSequence = sequence;
            if (RaidSession.Instance == null || RaidSession.Instance.CurrentState != RaidSessionState.Combat ||
                Action.Value != RaidAction.Free || Time.time < attackUntil || Time.time < dodgeUntil || RaidBoss.Instance == null)
            { Debug.Log("RAID_ACTION_REJECTED: attack / unavailable state"); return; }
            if (strong && Stamina.Value < strongCost)
            { Debug.Log("RAID_ACTION_REJECTED: strong attack / stamina"); return; }
            if (RaidBoss.Instance.DistanceToBodySurface(transform.position) > attackRange)
            { Debug.Log("RAID_ACTION_REJECTED: attack / out of range"); return; }
            if (strong) Stamina.Value -= strongCost;
            Action.Value = strong ? RaidAction.StrongAttack : RaidAction.Attack;
            attackUntil = Time.time + (strong ? 0.9f : 0.55f);
            RaidBoss.Instance.ReceiveHit(LockedPart.Value, strong ? strongBasePower : weakBasePower, attackPower,
                strong ? strongPartPower : weakPartPower);
        }

        [Rpc(SendTo.Server)]
        public void SetReviveIntentRpc(bool held) { reviveHeld = held; }

        public bool ReviveIntent => reviveHeld;
        public void SetTestReviveHold(bool held)
        {
            testReviveHold = held;
            if (IsServer) reviveHeld = held;
        }

        [Rpc(SendTo.Server)]
        void RequestStanceRpc()
        {
            if (RaidSession.Instance == null ||
                (RaidSession.Instance.CurrentState != RaidSessionState.Combat &&
                 RaidSession.Instance.CurrentState != RaidSessionState.Tutorial) ||
                Action.Value != RaidAction.Free || StanceChanging.Value || Time.time < nextStanceAllowed)
            { Debug.Log("COOP_REJECTED: stance player=" + OwnerClientId); return; }
            pendingStance = Stance.Value == ChainStance.Flexible ? ChainStance.Braced : ChainStance.Flexible;
            stanceChangeAt = Time.time + stanceTransitionSeconds;
            nextStanceAllowed = Time.time + stanceCooldownSeconds;
            StanceChanging.Value = true;
        }

        [Rpc(SendTo.Server)]
        void SetPullIntentRpc(bool held, double estimatedServerTime)
        {
            if (RaidSession.Instance == null ||
                (RaidSession.Instance.CurrentState != RaidSessionState.Combat &&
                 RaidSession.Instance.CurrentState != RaidSessionState.Tutorial))
            { PullHeld.Value = false; return; }
            if (held && !CanCooperate) { PullHeld.Value = false; return; }
            PullHeld.Value = held;
            double now = NetworkManager.ServerTime.Time;
            PullHeldSince = held ? System.Math.Max(now - .1d, System.Math.Min(estimatedServerTime, now)) : 0d;
        }

        public void SetTestCooperation(ChainStance stance, bool pulling)
        {
            if (!IsServer) return;
            Stance.Value = stance;
            StanceChanging.Value = false;
            PullHeld.Value = pulling;
            PullHeldSince = pulling ? NetworkManager.ServerTime.Time : 0d;
        }

        public void CancelPullOnServer()
        {
            if (!IsServer) return;
            PullHeld.Value = false;
            PullHeldSince = 0d;
        }

        [Rpc(SendTo.Server)]
        void SetStruggleIntentRpc(bool held)
        {
            StruggleHeld.Value = held && Action.Value == RaidAction.Grabbed;
        }

        public bool SetGrabbedByBoss()
        {
            if (!IsServer || IsDown || Action.Value == RaidAction.Grabbed) return false;
            Action.Value = RaidAction.Grabbed;
            CancelPullOnServer();
            StruggleHeld.Value = false;
            return true;
        }

        public void ReleaseBossGrab()
        {
            if (!IsServer || Action.Value != RaidAction.Grabbed) return;
            Action.Value = RaidAction.Free;
            StruggleHeld.Value = false;
        }

        public void StartForcedMove(Vector3 direction, float distance, float speedLimit)
        {
            if (!IsServer || IsDown) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) return;
            forcedMoveDirection = direction.normalized;
            forcedMoveRemaining = Mathf.Max(0f, distance);
            forcedMoveSpeed = Mathf.Max(.1f, speedLimit);
        }

        public void ReceiveDamage(float basePower, float enemyAttack, bool knockdown = false)
        {
            if (!IsServer || IsDown || Time.time < dodgeUntil) return;
            CancelPullOnServer();
            StanceChanging.Value = false;
            forcedMoveRemaining = 0f;
            Health.Value = Mathf.Max(0f, Health.Value - DamageCalculator.Calculate(basePower, enemyAttack, defense));
            if (Health.Value <= 0f)
            {
                DownCount.Value++;
                ReviveProgress.Value = 0f;
                Action.Value = RaidAction.Incapacitated;
                reviveHeld = false;
            }
            else { Action.Value = knockdown ? RaidAction.Knockdown : RaidAction.HitReaction;
                attackUntil = Time.time + (knockdown ? 1f : 0.25f); }
        }

        public void TickRevive(float dt, bool valid)
        {
            if (!IsServer || !IsDown) return;
            ReviveProgress.Value = valid ? ReviveProgress.Value + dt : 0f;
            if (ReviveProgress.Value < RequiredReviveSeconds) return;
            Health.Value = maxHealth * reviveHealthFraction;
            ReviveProgress.Value = 0f;
            Action.Value = RaidAction.Free;
        }

        public void DrainChainStamina(float amount)
        {
            if (!IsServer || IsDown) return;
            Stamina.Value = Mathf.Max(0f, Stamina.Value - Mathf.Max(0f, amount));
        }

        public void ChainKnockdown()
        {
            if (!IsServer || IsDown || Action.Value == RaidAction.Grabbed ||
                Action.Value == RaidAction.Knockdown) return;
            Action.Value = RaidAction.Knockdown;
            attackUntil = Time.time + 1f;
        }

        public void ResetForLobby()
        {
            if (!IsServer) return;
            Health.Value = maxHealth;
            Stamina.Value = maxStamina;
            DownCount.Value = 0;
            ReviveProgress.Value = 0f;
            Action.Value = RaidAction.Free;
            reviveHeld = false;
            lastSentReviveHeld = false;
            LockEnabled.Value = false;
            Stance.Value = ChainStance.Flexible;
            StanceChanging.Value = false;
            PullHeld.Value = false;
            PullHeldSince = 0d;
            StruggleHeld.Value = false;
            forcedMoveRemaining = 0f;
            nextStanceAllowed = 0f;
        }

        public void ResetForTutorial()
        {
            if (!IsServer) return;
            Health.Value = maxHealth;
            Stamina.Value = maxStamina;
            Action.Value = RaidAction.Free;
            ReviveProgress.Value = 0f;
            forcedMoveRemaining = 0f;
        }
    }
}
