using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public enum BossAttack { None, Stomp, Charge, TrunkSweep, ChainSnatch, SpinGrab }
    public enum BossAttackStage { Idle, Telegraph, Active, Recovery }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class RaidBoss : NetworkBehaviour
    {
        public static RaidBoss Instance { get; private set; }
        public static readonly Vector3 FrontLegAnchorLocal = new Vector3(.5f, -.18f, 1f);
        public static readonly Vector3 TrunkAnchorLocal = new Vector3(0f, .25f, 1.6f);
        public RaidBossSettings settings;
        public float maxHealth = 1000f;
        public float attackPower = 35f;
        public float defense = 15f;
        public float partMaxDurability = 180f;
        public float stompBasePower = 30f;
        public readonly NetworkVariable<float> Health = new NetworkVariable<float>();
        public readonly NetworkVariable<float> TrunkDurability = new NetworkVariable<float>();
        public readonly NetworkVariable<float> FrontLegDurability = new NetworkVariable<float>();
        public readonly NetworkVariable<int> StaggerCount = new NetworkVariable<int>();
        public readonly NetworkVariable<bool> Telegraphing = new NetworkVariable<bool>();
        public readonly NetworkVariable<int> StompSequence = new NetworkVariable<int>();
        public readonly NetworkVariable<bool> Staggered = new NetworkVariable<bool>();
        public readonly NetworkVariable<int> Phase = new NetworkVariable<int>();
        public readonly NetworkVariable<BossAttack> CurrentAttack = new NetworkVariable<BossAttack>();
        public readonly NetworkVariable<BossAttackStage> Stage = new NetworkVariable<BossAttackStage>();
        public readonly NetworkVariable<int> AttackSequence = new NetworkVariable<int>();
        public readonly NetworkVariable<double> TelegraphEndServerTime = new NetworkVariable<double>();
        public readonly NetworkVariable<Vector3> AttackOrigin = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<Vector3> AttackDirection = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<ulong> TargetClientId = new NetworkVariable<ulong>(ulong.MaxValue);
        public readonly NetworkVariable<float> SpinRescueProgress = new NetworkVariable<float>();
        public readonly NetworkVariable<bool> SnatchPulling = new NetworkVariable<bool>();
        public readonly NetworkVariable<float> SpinSpeedFactor = new NetworkVariable<float>(1f);

        readonly HashSet<ulong> hitThisAttack = new HashSet<ulong>();
        float stageEndTime;
        float staggerUntil;
        float nextAttackTime;
        int selectionIndex;
        bool snatchResolving;
        RaidPlayer grabbedPlayer;
        float spinRescueHeld;
        public int SnatchAvoidCount { get; private set; }
        public int SpinRescueCount { get; private set; }
        Vector3 initialPosition;
        Quaternion initialRotation;

        public Vector3 RescuePoint => transform.position + transform.right * 3f;

        public float DistanceToBodySurface(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            float side = Mathf.Abs(Vector3.Dot(offset, transform.right));
            float forward = Vector3.Dot(offset, transform.forward);
            float outsideSide = Mathf.Max(0f, side - 2.6f);
            float outsideLength = Mathf.Max(0f, forward > 0f ? forward - 4.5f : -3.5f - forward);
            return new Vector2(outsideSide, outsideLength).magnitude;
        }

        void Awake()
        {
            Instance = this;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            Health.Value = maxHealth;
            TrunkDurability.Value = partMaxDurability;
            FrontLegDurability.Value = partMaxDurability;
            Phase.Value = 1;
            ResetAttack();
            nextAttackTime = Time.time + (settings != null ? settings.firstAttackDelay : 3f);
            Staggered.Value = false;
        }

        void Update()
        {
            if (!IsServer || settings == null) return;
            bool tutorial = RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Tutorial;
            if (RaidSession.Instance == null ||
                (RaidSession.Instance.CurrentState != RaidSessionState.Combat && !tutorial) || Health.Value <= 0f)
            {
                if (Stage.Value != BossAttackStage.Idle) CancelAttack();
                return;
            }
            if (Phase.Value == 1 && Health.Value <= maxHealth * .5f)
            { Phase.Value = 2; Debug.Log("BOSS_EVENT: PhaseChanged phase=2"); }
            if (Staggered.Value)
            {
                if (Time.time < staggerUntil) return;
                Staggered.Value = false;
            }
            switch (Stage.Value)
            {
                case BossAttackStage.Idle:
                    if (!tutorial) FaceTarget();
                    if (!tutorial && Time.time >= nextAttackTime) StartAttack(SelectAttack());
                    break;
                case BossAttackStage.Telegraph:
                    if (Time.time >= stageEndTime) BeginActive();
                    break;
                case BossAttackStage.Active:
                    TickActive();
                    break;
                case BossAttackStage.Recovery:
                    if (Time.time >= stageEndTime)
                    { Stage.Value = BossAttackStage.Idle; CurrentAttack.Value = BossAttack.None; }
                    break;
            }
        }

        BossAttack SelectAttack()
        {
            BossAttack[] phaseOne = { BossAttack.Stomp, BossAttack.TrunkSweep, BossAttack.Charge,
                BossAttack.ChainSnatch, BossAttack.Stomp, BossAttack.SpinGrab };
            BossAttack[] phaseTwo = { BossAttack.ChainSnatch, BossAttack.Charge,
                BossAttack.SpinGrab, BossAttack.Stomp, BossAttack.ChainSnatch, BossAttack.TrunkSweep };
            var order = Phase.Value == 1 ? phaseOne : phaseTwo;
            return order[selectionIndex++ % order.Length];
        }

        public void ForceAttackForTest(BossAttack attack)
        {
            if (!IsServer || settings == null || RaidSession.Instance == null ||
                RaidSession.Instance.CurrentState != RaidSessionState.Combat) return;
            CancelAttack();
            Staggered.Value = false;
            StartAttack(attack);
        }

        public void ForceTutorialAttack(BossAttack attack, RaidPlayer target)
        {
            if (!IsServer || RaidSession.Instance == null ||
                RaidSession.Instance.CurrentState != RaidSessionState.Tutorial || target == null) return;
            CancelAttack();
            Staggered.Value = false;
            StartAttack(attack);
            TargetClientId.Value = target.OwnerClientId;
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .01f)
            { AttackDirection.Value = direction.normalized; transform.rotation = Quaternion.LookRotation(direction); }
        }

        void StartAttack(BossAttack attack)
        {
            if (attack == BossAttack.None) return;
            CurrentAttack.Value = attack;
            Stage.Value = BossAttackStage.Telegraph;
            AttackSequence.Value++;
            hitThisAttack.Clear();
            snatchResolving = false;
            SnatchPulling.Value = false;
            SpinRescueProgress.Value = 0f;
            AttackOrigin.Value = transform.position;
            RaidPlayer target = ChooseTarget();
            TargetClientId.Value = target != null ? target.OwnerClientId : ulong.MaxValue;
            Vector3 direction = target != null ? target.transform.position - transform.position : transform.forward;
            direction.y = 0f;
            AttackDirection.Value = direction.sqrMagnitude > .001f ? direction.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(AttackDirection.Value);
            float duration = attack switch
            {
                BossAttack.Stomp => settings.stompTelegraph,
                BossAttack.Charge => settings.chargeTelegraph,
                BossAttack.TrunkSweep => settings.sweepTelegraph,
                BossAttack.ChainSnatch => settings.snatchTelegraph,
                _ => settings.spinTelegraph
            };
            stageEndTime = Time.time + duration;
            TelegraphEndServerTime.Value = NetworkManager.ServerTime.Time + duration;
            Telegraphing.Value = true;
            Debug.Log("BOSS_EVENT: Telegraph attack=" + attack + " id=" + AttackSequence.Value +
                " target=" + TargetClientId.Value);
        }

        RaidPlayer ChooseTarget()
        {
            RaidPlayer target = null;
            foreach (var player in RaidSession.Instance.Players)
            {
                if (player == null || player.IsDown) continue;
                if (target == null || Vector3.Distance(player.transform.position, transform.position) <
                    Vector3.Distance(target.transform.position, transform.position)) target = player;
            }
            return target;
        }

        void FaceTarget()
        {
            var target = ChooseTarget();
            if (target == null) return;
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 150f * Time.deltaTime);
        }

        RaidPlayer TargetPlayer()
        {
            foreach (var player in RaidSession.Instance.Players)
                if (player != null && player.OwnerClientId == TargetClientId.Value) return player;
            return null;
        }

        void BeginActive()
        {
            Telegraphing.Value = false;
            Stage.Value = BossAttackStage.Active;
            Debug.Log("BOSS_EVENT: Active attack=" + CurrentAttack.Value + " id=" + AttackSequence.Value);
            switch (CurrentAttack.Value)
            {
                case BossAttack.Stomp:
                    StompSequence.Value++;
                    foreach (var player in RaidSession.Instance.Players)
                        if (player != null && FlatDistance(player.transform.position, AttackOrigin.Value) <= settings.stompRadius)
                            HitOnce(player, settings.stompPower, true);
                    Recover();
                    break;
                case BossAttack.TrunkSweep:
                    foreach (var player in RaidSession.Instance.Players)
                    {
                        if (player == null || FlatDistance(player.transform.position, AttackOrigin.Value) > settings.sweepRadius) continue;
                        Vector3 relative = player.transform.position - AttackOrigin.Value; relative.y = 0f;
                        if (Vector3.Angle(AttackDirection.Value, relative) > settings.sweepHalfAngle) continue;
                        HitOnce(player, settings.sweepPower, false);
                        player.StartForcedMove(relative, settings.sweepPushDistance, settings.forcedMoveSpeedLimit);
                    }
                    Recover();
                    break;
                case BossAttack.Charge:
                    stageEndTime = Time.time + settings.chargeDistance / Mathf.Max(.1f, settings.chargeSpeed);
                    break;
                case BossAttack.ChainSnatch:
                    if (RaidChain.Instance == null || RaidChain.Instance.PathLength.Value < RaidChain.Instance.warningLength ||
                        !RaidChain.Instance.BeginSnatch())
                    { SnatchAvoidCount++; Debug.Log("BOSS_EVENT: SnatchAvoided"); Recover(); }
                    else stageEndTime = Time.time + settings.snatchResponse;
                    break;
                case BossAttack.SpinGrab:
                    grabbedPlayer = TargetPlayer();
                    if (grabbedPlayer == null || FlatDistance(grabbedPlayer.transform.position, transform.position) > settings.spinGrabRange ||
                        !grabbedPlayer.SetGrabbedByBoss())
                    { Debug.Log("BOSS_EVENT: SpinAvoided"); grabbedPlayer = null; Recover(); }
                    else
                    {
                        stageEndTime = Time.time + settings.spinDuration;
                        spinRescueHeld = 0f;
                        SpinSpeedFactor.Value = 1f;
                    }
                    break;
            }
        }

        void TickActive()
        {
            switch (CurrentAttack.Value)
            {
                case BossAttack.Charge:
                    transform.position += AttackDirection.Value * settings.chargeSpeed * Time.deltaTime;
                    Vector3 chargeBack = transform.position - transform.forward * settings.chargeBodyBack;
                    Vector3 chargeFront = transform.position + transform.forward * settings.chargeBodyFront;
                    foreach (var player in RaidSession.Instance.Players)
                        if (player != null && FlatDistanceToSegment(player.transform.position,
                            chargeBack, chargeFront) <= settings.chargeWidth)
                            HitOnce(player, settings.chargePower, false);
                    if (Mathf.Abs(transform.position.x) >= 14f || Mathf.Abs(transform.position.z) >= 11f)
                    { ApplyCooperationStagger(settings.staggerSeconds); return; }
                    if (Time.time >= stageEndTime) Recover();
                    break;
                case BossAttack.ChainSnatch:
                    if (RaidChain.Instance == null || RaidChain.Instance.ActiveAnchor.Value != RaidChainAnchor.Trunk)
                    { Recover(); return; }
                    if (Time.time < stageEndTime) return;
                    if (!snatchResolving)
                    {
                        snatchResolving = true;
                        SnatchPulling.Value = true;
                        stageEndTime = Time.time + settings.snatchPullDistance /
                            Mathf.Max(.1f, settings.forcedMoveSpeedLimit);
                        foreach (var player in RaidSession.Instance.Players)
                            if (player != null) player.StartForcedMove(transform.position - player.transform.position,
                                settings.snatchPullDistance, settings.forcedMoveSpeedLimit);
                        return;
                    }
                    foreach (var player in RaidSession.Instance.Players)
                        if (player != null) HitOnce(player, settings.snatchPower, false);
                    RaidChain.Instance.EndSnatch();
                    Recover();
                    break;
                case BossAttack.SpinGrab:
                    if (grabbedPlayer == null || grabbedPlayer.Action.Value != RaidAction.Grabbed)
                    { Recover(); return; }
                    TickSpinRescue();
                    if (Stage.Value != BossAttackStage.Active || Time.time < stageEndTime) return;
                    RaidPlayer free = FreePlayer();
                    if (free != null && RaidChain.Instance != null &&
                        RaidChain.Instance.PathLength.Value >= RaidChain.Instance.warningLength)
                        HitOnce(free, settings.spinTensionPower, false);
                    bool slowed = grabbedPlayer.StruggleHeld.Value;
                    grabbedPlayer.ReleaseBossGrab();
                    HitOnce(grabbedPlayer, settings.spinLandingPower * (slowed ? .75f : 1f), false);
                    grabbedPlayer = null;
                    Recover();
                    break;
            }
        }

        void TickSpinRescue()
        {
            RaidPlayer free = FreePlayer();
            bool valid = free != null && free.CanCooperate && free.ReviveIntent &&
                FlatDistance(free.transform.position, RescuePoint) <= settings.spinRescueRadius;
            if (valid)
            {
                if (spinRescueHeld <= 0f) Debug.Log("BOSS_EVENT: SpinRescueStarted");
                spinRescueHeld += Time.deltaTime;
            }
            else if (spinRescueHeld > 0f)
            { spinRescueHeld = 0f; Debug.Log("BOSS_EVENT: SpinRescueCancelled"); }
            SpinRescueProgress.Value = spinRescueHeld;
            SpinSpeedFactor.Value = grabbedPlayer.StruggleHeld.Value ? .65f : 1f;
            if (spinRescueHeld < settings.spinRescueSeconds) return;
            grabbedPlayer.ReleaseBossGrab();
            grabbedPlayer = null;
            Debug.Log("BOSS_EVENT: SpinRescueCompleted");
            SpinRescueCount++;
            Recover();
        }

        RaidPlayer FreePlayer()
        {
            foreach (var player in RaidSession.Instance.Players)
                if (player != grabbedPlayer) return player;
            return null;
        }

        void HitOnce(RaidPlayer player, float power, bool knockdown)
        {
            if (!hitThisAttack.Add(player.OwnerClientId)) return;
            player.ReceiveDamage(power, attackPower, knockdown);
            Debug.Log("BOSS_EVENT: Hit attack=" + CurrentAttack.Value + " player=" + player.OwnerClientId +
                " id=" + AttackSequence.Value);
        }

        void Recover()
        {
            if (grabbedPlayer != null) grabbedPlayer.ReleaseBossGrab();
            grabbedPlayer = null;
            if (RaidChain.Instance != null && RaidChain.Instance.ActiveAnchor.Value == RaidChainAnchor.Trunk)
                RaidChain.Instance.EndSnatch();
            Stage.Value = BossAttackStage.Recovery;
            Telegraphing.Value = false;
            SnatchPulling.Value = false;
            SpinRescueProgress.Value = 0f;
            stageEndTime = Time.time + Mathf.Max(2f, settings.recoverySeconds);
            nextAttackTime = stageEndTime;
            Debug.Log("BOSS_EVENT: Recovery attack=" + CurrentAttack.Value + " id=" + AttackSequence.Value);
        }

        void CancelAttack()
        {
            if (grabbedPlayer != null) grabbedPlayer.ReleaseBossGrab();
            grabbedPlayer = null;
            if (RaidChain.Instance != null && RaidChain.Instance.ActiveAnchor.Value == RaidChainAnchor.Trunk)
                RaidChain.Instance.EndSnatch();
            ResetAttack();
        }

        void ResetAttack()
        {
            CurrentAttack.Value = BossAttack.None;
            Stage.Value = BossAttackStage.Idle;
            TargetClientId.Value = ulong.MaxValue;
            Telegraphing.Value = false;
            SnatchPulling.Value = false;
            SpinRescueProgress.Value = 0f;
            SpinSpeedFactor.Value = 1f;
            snatchResolving = false;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static float FlatDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            point.y = start.y = end.y = 0f;
            Vector3 segment = end - start;
            float t = segment.sqrMagnitude > .001f ?
                Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude) : 0f;
            return Vector3.Distance(point, start + segment * t);
        }

        public void ReceiveHit(BossPart part, float basePower, float attackerPower, float partPower)
        {
            if (!IsServer || Health.Value <= 0f || RaidSession.Instance == null ||
                RaidSession.Instance.CurrentState != RaidSessionState.Combat) return;
            Health.Value = Mathf.Max(0f, Health.Value - DamageCalculator.Calculate(basePower, attackerPower, defense));
            if (part == BossPart.Trunk)
            {
                bool intact = TrunkDurability.Value > 0f;
                TrunkDurability.Value = Mathf.Max(0f, TrunkDurability.Value - partPower);
                if (intact && TrunkDurability.Value == 0f)
                    ApplyCooperationStagger(settings != null ? settings.staggerSeconds : 3f);
            }
            else if (part == BossPart.FrontLeg)
            {
                bool intact = FrontLegDurability.Value > 0f;
                FrontLegDurability.Value = Mathf.Max(0f, FrontLegDurability.Value - partPower);
                if (intact && FrontLegDurability.Value == 0f)
                    ApplyCooperationStagger(settings != null ? settings.staggerSeconds : 3f);
            }
        }

        public void ApplyCooperationStagger(float seconds)
        {
            if (!IsServer || Health.Value <= 0f) return;
            CancelAttack();
            StaggerCount.Value++;
            Staggered.Value = true;
            staggerUntil = Time.time + Mathf.Max(.1f, seconds);
            nextAttackTime = staggerUntil + 2f;
            Debug.Log("COOP_EVENT: StaggerTriggered count=" + StaggerCount.Value);
        }

        public void ResetForLobby()
        {
            if (!IsServer) return;
            CancelAttack();
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            Health.Value = maxHealth;
            TrunkDurability.Value = partMaxDurability;
            FrontLegDurability.Value = partMaxDurability;
            StaggerCount.Value = 0;
            Telegraphing.Value = false;
            StompSequence.Value = 0;
            Staggered.Value = false;
            staggerUntil = 0f;
            Phase.Value = 1;
            selectionIndex = 0;
            nextAttackTime = Time.time + (settings != null ? settings.firstAttackDelay : 3f);
        }
    }
}
