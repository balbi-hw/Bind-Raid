using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public sealed partial class RaidPlayer
    {
        [Header("Weapon")]
        public RaidWeaponSettings weaponSettings;
        public readonly NetworkVariable<RaidAttackPlayback> AttackPlayback = new NetworkVariable<RaidAttackPlayback>();
        public event System.Action<Vector3> HitConfirmed;
        readonly RaidAttackHitResolver hitResolver = new RaidAttackHitResolver();
        int queuedAttack = -1;
        double queuedAttackExpires;
        uint nextAttackInstance;
        uint lastFeedbackInstance;
        Vector3 lastFeedbackPoint;

        public RaidAttackDefinition PresentedAttack => weaponSettings != null && AttackPlayback.Value.Active ?
            weaponSettings.Get(AttackPlayback.Value.Definition) : null;
        public float AttackNormalizedTime => PresentedAttack == null ? 0f :
            Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - AttackPlayback.Value.StartedAt) / PresentedAttack.Duration);

        [Rpc(SendTo.Server)]
        void AttackRpc(bool strong, int sequence)
        {
            if (sequence <= lastAttackSequence) return;
            lastAttackSequence = sequence;
            if (!CanAttackInSession() || weaponSettings == null)
            { Debug.Log("RAID_ACTION_REJECTED: attack / session or weapon"); return; }
            var current = PresentedAttack;
            double now = NetworkManager.ServerTime.Time;
            if (current != null)
            {
                if (Action.Value != (current.strong ? RaidAction.StrongAttack : RaidAction.Attack) ||
                    !current.CanBuffer(now - AttackPlayback.Value.StartedAt))
                { Debug.Log("RAID_ACTION_REJECTED: attack / buffer closed"); return; }
                int next = strong ? weaponSettings.strongAttack : current.nextWeakAttack;
                if (weaponSettings.Get(next) == null) return;
                // One slot, last deliberate input wins. Expiry is measured by the server.
                queuedAttack = next;
                queuedAttackExpires = now + Mathf.Max(0f, current.bufferLifetime);
                return;
            }
            StartAttackOnServer(strong ? weaponSettings.strongAttack : weaponSettings.weakAttack);
        }

        bool CanAttackInSession() => RaidSession.Instance != null &&
            RaidSession.Instance.CurrentState == RaidSessionState.Combat;

        void StartAttackOnServer(int definition)
        {
            var attack = weaponSettings.Get(definition);
            if (attack == null || Action.Value != RaidAction.Free || StanceChanging.Value ||
                Stamina.Value < attack.staminaCost || !CanAttackInSession())
            { Debug.Log("RAID_ACTION_REJECTED: attack / state or stamina"); return; }
            Stamina.Value -= attack.staminaCost;
            CancelPullOnServer();
            Action.Value = attack.strong ? RaidAction.StrongAttack : RaidAction.Attack;
            motorState.ActionRemaining = attack.Duration;
            hitResolver.Reset();
            AttackPlayback.Value = new RaidAttackPlayback {
                Instance = ++nextAttackInstance, Definition = definition,
                StartedAt = NetworkManager.ServerTime.Time, Heading = transform.forward, Active = true
            };
            Debug.Log("ATTACK_EVENT: Started id=" + nextAttackInstance + " definition=" + attack.id);
        }

        void TickAttackBeforeMove(ref RaidMoveInput input, float dt)
        {
            if (queuedAttack >= 0 && NetworkManager.ServerTime.Time > queuedAttackExpires) queuedAttack = -1;
            var attack = PresentedAttack;
            if (attack == null) return;
            double elapsed = NetworkManager.ServerTime.Time - AttackPlayback.Value.StartedAt;
            if (!CanAttackInSession() || Action.Value != (attack.strong ? RaidAction.StrongAttack : RaidAction.Attack))
            { CancelAttackOnServer(); return; }
            if (input.Dodge && elapsed >= attack.dodgeCancelOpen && controller.isGrounded &&
                Stamina.Value >= movementSettings.dodgeCost)
            {
                CancelAttackOnServer("Dodge");
                Action.Value = RaidAction.Free;
                return;
            }
            if (elapsed >= attack.Duration)
            {
                int next = queuedAttack;
                queuedAttack = -1;
                EndAttackPlayback();
                Action.Value = RaidAction.Free;
                if (next >= 0) StartAttackOnServer(next);
                attack = PresentedAttack;
                if (attack == null) return;
                elapsed = 0d;
            }
            // The attack clock owns its lifetime; the movement motor must not end it early.
            motorState.ActionRemaining = Mathf.Max(dt, attack.Duration - (float)elapsed + dt);
        }

        void TickAttackHits()
        {
            var attack = PresentedAttack;
            if (attack == null || !CanAttackInSession() ||
                Action.Value != (attack.strong ? RaidAction.StrongAttack : RaidAction.Attack)) return;
            double elapsed = NetworkManager.ServerTime.Time - AttackPlayback.Value.StartedAt;
            if (!attack.IsActive(elapsed)) return;
            var request = new RaidHitRequest {
                Attacker = NetworkObjectId, AttackInstance = AttackPlayback.Value.Instance,
                AttackId = attack.id, BasePower = attack.basePower, AttackPower = attackPower, PartPower = attack.partPower
            };
            hitResolver.Resolve(transform, attack, weaponSettings, request, LockEnabled.Value ? (int)LockedPart.Value : -1,
                hit => {
                    Debug.Log("ATTACK_EVENT: Hit id=" + hit.AttackInstance + " part=" + hit.Part);
                    ConfirmHitRpc(hit.AttackInstance, hit.Point);
                });
        }

        void EndAttackPlayback()
        {
            var playback = AttackPlayback.Value;
            playback.Active = false;
            AttackPlayback.Value = playback;
            hitResolver.Reset();
        }

        void CancelAttackOnServer(string reason = "Interrupted")
        {
            if (!IsServer) return;
            queuedAttack = -1;
            if (AttackPlayback.Value.Active)
            {
                Debug.Log("ATTACK_EVENT: Cancelled id=" + AttackPlayback.Value.Instance + " reason=" + reason);
                EndAttackPlayback();
            }
        }

        [Rpc(SendTo.Server)]
        void ClearAttackBufferRpc() { queuedAttack = -1; }

        [Rpc(SendTo.ClientsAndHost)]
        void ConfirmHitRpc(uint instance, Vector3 point)
        {
            if (lastFeedbackInstance == instance && lastFeedbackPoint == point) return;
            lastFeedbackInstance = instance;
            lastFeedbackPoint = point;
            HitConfirmed?.Invoke(point);
        }
    }
}
