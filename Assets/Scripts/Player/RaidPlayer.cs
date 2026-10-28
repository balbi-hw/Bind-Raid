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
        public float attackRange = 4.5f;

        [Header("Keyboard and mouse")]
        public KeyCode forward = KeyCode.W, back = KeyCode.S, left = KeyCode.A, right = KeyCode.D;
        public KeyCode sprint = KeyCode.LeftShift, dodge = KeyCode.Space, weakAttack = KeyCode.Mouse0;
        public KeyCode strongAttack = KeyCode.Mouse1, lockOn = KeyCode.Tab, revive = KeyCode.E;

        public readonly NetworkVariable<float> Health = new NetworkVariable<float>();
        public readonly NetworkVariable<float> Stamina = new NetworkVariable<float>();
        public readonly NetworkVariable<int> DownCount = new NetworkVariable<int>();
        public readonly NetworkVariable<RaidAction> Action = new NetworkVariable<RaidAction>();
        public readonly NetworkVariable<float> ReviveProgress = new NetworkVariable<float>();
        public readonly NetworkVariable<BossPart> LockedPart = new NetworkVariable<BossPart>();
        public readonly NetworkVariable<bool> LockEnabled = new NetworkVariable<bool>();

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

        public bool IsDown => Action.Value == RaidAction.Incapacitated;
        public float RequiredReviveSeconds => ReviveRules.RequiredSeconds(DownCount.Value);

        void Awake() { controller = GetComponent<CharacterController>(); }

        public override void OnNetworkSpawn()
        {
            Debug.Log("RAID_TEST: player spawned owner=" + OwnerClientId + " local=" + IsOwner);
            if (IsServer) { Health.Value = maxHealth; Stamina.Value = maxStamina; Action.Value = RaidAction.Free; }
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
            if (!IsSpawned || !IsOwner || RaidSession.Instance == null ||
                RaidSession.Instance.CurrentState != RaidSessionState.Combat) return;

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
            bool held = testReviveHold || Input.GetKey(revive);
            if (held != lastSentReviveHeld) { lastSentReviveHeld = held; SetReviveIntentRpc(held); }
        }

        [Rpc(SendTo.Server)]
        void SubmitMoveRpc(Vector3 move, bool running, bool roll)
        {
            if (RaidSession.Instance == null || RaidSession.Instance.CurrentState != RaidSessionState.Combat || IsDown)
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
            Vector3 horizontal = dodging ? dodgeDirection * dodgeSpeed : move * (sprinting ? sprintSpeed : walkSpeed);
            if (horizontal.sqrMagnitude > 0.01f) transform.forward = Vector3.Slerp(transform.forward, horizontal.normalized, 12f * dt);
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + gravity * dt;
            controller.Move((horizontal + Vector3.up * verticalVelocity) * dt);
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
            if (Vector3.Distance(transform.position, RaidBoss.Instance.transform.position) > attackRange)
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
        public void SetTestReviveHold(bool held) { testReviveHold = held; }

        public void ReceiveDamage(float basePower, float enemyAttack, bool knockdown = false)
        {
            if (!IsServer || IsDown || Time.time < dodgeUntil) return;
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
        }
    }
}
