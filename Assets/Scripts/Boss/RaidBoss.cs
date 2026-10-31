using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public enum BossAttack { None, Stomp, Charge, TrunkSweep, ChainSnatch, SpinGrab }
    public enum BossAttackStage { Idle, Telegraph, Active, Recovery }

    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class RaidBoss : NetworkBehaviour, IRaidDamageReceiver
    {
        public static RaidBoss Instance { get; private set; }
        public Vector3 FrontLegAnchorLocal => settings != null && settings.anchors != null ? settings.anchors.frontLeg : Vector3.zero;
        public Vector3 TrunkAnchorLocal => settings != null && settings.anchors != null ? settings.anchors.trunk : Vector3.zero;
        public RaidBossSettings settings;
        public readonly NetworkVariable<double> StageStartedAt = new NetworkVariable<double>();
        public readonly NetworkVariable<float> StageDuration = new NetworkVariable<float>();
        public readonly NetworkVariable<double> HitFlashUntil = new NetworkVariable<double>();
        bool hurtboxesBuilt;
        RaidTargetFocus focusTarget;
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
        readonly Dictionary<BossAttack, float> moveReadyAt = new Dictionary<BossAttack, float>();
        BossAttack previousAttack;
        float controlReadyAt;
        Collider bodyCollider;
        bool snatchResolving;
        RaidPlayer grabbedPlayer;
        float spinRescueHeld;
        public int SnatchAvoidCount { get; private set; }
        public int SpinRescueCount { get; private set; }
        public bool TestPassive { get; set; }
        Vector3 initialPosition;
        Quaternion initialRotation;

        public Vector3 RescuePoint => transform.TransformPoint(settings != null && settings.anchors != null ? settings.anchors.rescue : Vector3.zero);
        public bool TryGetChainAnchor(RaidChainAnchor anchor, out Vector3 point)
        {
            point = transform.position;
            if (settings == null || settings.anchors == null || Health.Value <= 0f) return false;
            if (anchor == RaidChainAnchor.FrontLeg && FrontLegDurability.Value > 0f)
            { point = transform.TransformPoint(FrontLegAnchorLocal); return true; }
            if (anchor == RaidChainAnchor.Trunk)
            { point = transform.TransformPoint(TrunkAnchorLocal); return true; }
            return false;
        }

        void Awake()
        {
            Instance = this;
            bodyCollider = GetComponent<Collider>();
            InitializeMoves();
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
            if (!hurtboxesBuilt && settings != null && settings.hitboxLayout != null)
            {
                settings.hitboxLayout.Build(transform, this);
                hurtboxesBuilt = true;
            }
            focusTarget = GetComponent<RaidTargetFocus>();
            if (focusTarget == null) focusTarget = gameObject.AddComponent<RaidTargetFocus>();
            focusTarget.Refresh();
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
                    if (!tutorial && !TestPassive && Time.time >= nextAttackTime)
                    {
                        BossAttack selected = SelectAttack();
                        if (selected != BossAttack.None) StartAttack(selected);
                        else ApproachTarget();
                    }
                    break;
                case BossAttackStage.Telegraph:
                    var move = settings.moveSet != null ? settings.moveSet.Get(CurrentAttack.Value) : null;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(AttackDirection.Value),
                        (move != null ? move.turnDegreesPerSecond : 150f) * Time.deltaTime);
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
            if (settings.moveSet == null) return BossAttack.None;
            var target = ChooseTarget();
            Vector3 direction = target != null ? target.transform.position - transform.position : transform.forward;
            direction.y = 0f;
            var chain = RaidChain.Instance;
            var context = new RaidBossDecisionContext {
                distance = direction.magnitude, angle = Vector3.Angle(transform.forward, direction),
                now = Time.time, phase = Phase.Value, targetAvailable = target != null,
                chainAvailable = chain != null, chainTaut = chain != null && chain.PathLength.Value >= chain.warningLength,
                controlActive = grabbedPlayer != null || (chain != null && chain.ActiveAnchor.Value == RaidChainAnchor.Trunk),
                controlReadyAt = controlReadyAt, previous = previousAttack
            };
            return RaidBossDecision.Select(settings.moveSet, context, moveReadyAt, Random.value);
        }

        void ApproachTarget()
        {
            var target = ChooseTarget();
            if (target == null || settings.moveSet == null) return;
            Vector3 direction = target.transform.position - transform.position; direction.y = 0f;
            if (direction.magnitude > settings.moveSet.approachStopDistance)
                MoveBoss(direction.normalized * settings.moveSet.approachSpeed * Time.deltaTime);
        }

        bool MoveBoss(Vector3 displacement)
        {
            bool blocked = false;
            float distance = displacement.magnitude;
            if (distance < .00001f) return false;
            Vector3 direction = displacement / distance;
            if (bodyCollider != null)
            {
                var bounds = bodyCollider.bounds;
                Vector3 extents = Vector3.Max(bounds.extents - Vector3.one * .05f, Vector3.one * .05f);
                foreach (var hit in Physics.BoxCastAll(bounds.center, extents, direction, Quaternion.identity,
                    distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform) ||
                        hit.collider.GetComponentInParent<RaidPlayer>() != null || hit.normal.y > .5f) continue;
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .05f)); blocked = true;
                }
            }
            Vector3 desired = transform.position + direction * distance;
            if (settings.arena != null)
            {
                Vector3 clamped = settings.arena.Clamp(desired);
                blocked |= (clamped - desired).sqrMagnitude > .000001f;
                desired = clamped;
            }
            transform.position = desired;
            return blocked;
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
            { AttackDirection.Value = direction.normalized; }
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
            var moveDefinition = settings.moveSet != null ? settings.moveSet.Get(attack) : null;
            if (moveDefinition != null) moveReadyAt[attack] = Time.time + moveDefinition.cooldown;
            previousAttack = attack;
            float duration = attack switch
            {
                BossAttack.Stomp => settings.stompTelegraph,
                BossAttack.Charge => settings.chargeTelegraph,
                BossAttack.TrunkSweep => settings.sweepTelegraph,
                BossAttack.ChainSnatch => settings.snatchTelegraph,
                _ => settings.spinTelegraph
            };
            if (moveDefinition != null) duration = moveDefinition.telegraph;
            SetAttackDuration(duration);
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
                if (player == null || !RaidMovementMotor.CanMove(player.Action.Value)) continue;
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
                Quaternion.LookRotation(direction), (settings.moveSet != null ? settings.moveSet.idleTurnSpeed : 150f) * Time.deltaTime);
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
            if (moves.TryGetValue(CurrentAttack.Value, out var move)) move.begin?.Invoke();
        }

        void TickActive()
        {
            if (moves.TryGetValue(CurrentAttack.Value, out var move)) move.tick?.Invoke();
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
            var definition = settings.moveSet != null ? settings.moveSet.Get(CurrentAttack.Value) : null;
            float recovery = definition != null ? definition.recovery : settings.recoverySeconds;
            if (definition != null && settings.moveSet != null)
            {
                if ((definition.usesTrunk && TrunkDurability.Value <= 0f) || (definition.usesLeg && FrontLegDurability.Value <= 0f))
                    recovery *= settings.moveSet.brokenPartRecoveryMultiplier;
                if (definition.control) controlReadyAt = Time.time + settings.moveSet.controlRestSeconds;
            }
            SetAttackDuration(Mathf.Max(2f, recovery));
            nextAttackTime = stageEndTime;
            Debug.Log("BOSS_EVENT: Recovery attack=" + CurrentAttack.Value + " id=" + AttackSequence.Value);
        }

        void CancelAttack()
        {
            var cancelled = settings != null && settings.moveSet != null ? settings.moveSet.Get(CurrentAttack.Value) : null;
            if (cancelled != null && cancelled.control) controlReadyAt = Time.time + settings.moveSet.controlRestSeconds;
            if (grabbedPlayer != null) grabbedPlayer.ReleaseBossGrab();
            grabbedPlayer = null;
            if (RaidChain.Instance != null && RaidChain.Instance.ActiveAnchor.Value == RaidChainAnchor.Trunk)
                RaidChain.Instance.EndSnatch();
            ResetAttack();
        }

        void SetAttackDuration(float seconds)
        {
            stageEndTime = Time.time + seconds;
            StageStartedAt.Value = NetworkManager.ServerTime.Time;
            StageDuration.Value = seconds;
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

        public bool TryReceiveHit(RaidHitRequest hit)
        {
            if (!IsServer || Health.Value <= 0f || RaidSession.Instance == null ||
                RaidSession.Instance.CurrentState != RaidSessionState.Combat || hit.Part < 0 || hit.Part > (int)BossPart.FrontLeg) return false;
            BossPart part = (BossPart)hit.Part;
            float basePower = hit.BasePower, attackerPower = hit.AttackPower, partPower = hit.PartPower;
            HitFlashUntil.Value = NetworkManager.ServerTime.Time + .12d;
            Health.Value = Mathf.Max(0f, Health.Value - DamageCalculator.Calculate(basePower, attackerPower, defense));
            if (part == BossPart.Trunk)
            {
                bool intact = TrunkDurability.Value > 0f;
                TrunkDurability.Value = Mathf.Max(0f, TrunkDurability.Value - partPower);
                if (intact && TrunkDurability.Value == 0f)
                { focusTarget?.SetSelectable((int)BossPart.Trunk, false); ApplyCooperationStagger(settings != null ? settings.staggerSeconds : 3f); }
            }
            else if (part == BossPart.FrontLeg)
            {
                bool intact = FrontLegDurability.Value > 0f;
                FrontLegDurability.Value = Mathf.Max(0f, FrontLegDurability.Value - partPower);
                if (intact && FrontLegDurability.Value == 0f)
                { focusTarget?.SetSelectable((int)BossPart.FrontLeg, false); ApplyCooperationStagger(settings != null ? settings.staggerSeconds : 3f); }
            }
            return true;
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
            focusTarget?.ResetParts();
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
            moveReadyAt.Clear(); previousAttack = BossAttack.None; controlReadyAt = 0f;
            nextAttackTime = Time.time + (settings != null ? settings.firstAttackDelay : 3f);
        }
    }
}
