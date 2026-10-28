using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class RaidCooperation : NetworkBehaviour
    {
        public static RaidCooperation Instance { get; private set; }
        public RaidCooperationSettings settings;

        public readonly NetworkVariable<float> Gauge = new NetworkVariable<float>();
        public readonly NetworkVariable<RaidChainAnchor> Target = new NetworkVariable<RaidChainAnchor>();
        public readonly NetworkVariable<PullFailure> FailureReasons = new NetworkVariable<PullFailure>();
        public readonly NetworkVariable<int> EventSequence = new NetworkVariable<int>();

        float frontLegImmuneUntil;
        float trunkImmuneUntil;
        double validSince = -1d;
        bool attemptActive;
        PullFailure lastLoggedFailure;

        void Awake() { Instance = this; }
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        void Update()
        {
            if (!IsServer || settings == null) return;
            var session = RaidSession.Instance;
            if (session == null || session.CurrentState != RaidSessionState.Combat ||
                session.Players.Count != 2 || RaidBoss.Instance == null || RaidChain.Instance == null)
            { ResetForLobby(); return; }

            var first = session.Players[0];
            var second = session.Players[1];
            var anchor = RaidChain.Instance.ActiveAnchor.Value;
            bool holding = first.PullHeld.Value || second.PullHeld.Value;
            if (!holding)
            {
                if (attemptActive) Debug.Log("COOP_EVENT: PullAttemptCancelled");
                attemptActive = false;
                validSince = -1d;
                FailureReasons.Value = PullFailure.None;
                Gauge.Value = Mathf.Max(0f, Gauge.Value - settings.gaugeDecayPerSecond * Time.deltaTime);
                return;
            }
            if (!attemptActive)
            {
                attemptActive = true;
                session.ChainAttempts.Value++;
                EventSequence.Value++;
                Debug.Log("COOP_EVENT: PullAttemptStarted id=" + EventSequence.Value);
            }

            if (Target.Value != anchor)
            {
                Target.Value = anchor;
                Gauge.Value = 0f;
                validSince = -1d;
            }
            bool immune = anchor == RaidChainAnchor.FrontLeg ? Time.time < frontLegImmuneUntil :
                anchor == RaidChainAnchor.Trunk && Time.time < trunkImmuneUntil;
            float angle = RaidCooperationRules.Angle(RaidBoss.Instance.transform.position,
                first.transform.position, second.transform.position);
            PullFailure reasons = RaidCooperationRules.Evaluate(
                first.Stance.Value == ChainStance.Braced && !first.StanceChanging.Value,
                second.Stance.Value == ChainStance.Braced && !second.StanceChanging.Value,
                first.IsGrounded, second.IsGrounded,
                first.CanCooperate, second.CanCooperate,
                first.PullHeld.Value, second.PullHeld.Value,
                angle, settings.minAngle, settings.maxAngle,
                anchor != RaidChainAnchor.None, immune);
            FailureReasons.Value = reasons;
            if (reasons != PullFailure.None)
            {
                validSince = -1d;
                Gauge.Value = Mathf.Max(0f, Gauge.Value - settings.gaugeDecayPerSecond * Time.deltaTime);
                if (reasons != lastLoggedFailure)
                    Debug.Log("COOP_EVENT: PullAttemptFailed reasons=" + reasons + " angle=" + angle.ToString("F1"));
                lastLoggedFailure = reasons;
                return;
            }
            lastLoggedFailure = PullFailure.None;
            double now = NetworkManager.ServerTime.Time;
            if (validSince < 0d)
                validSince = System.Math.Max(now - .1d,
                    System.Math.Max(first.PullHeldSince, second.PullHeldSince));
            if (now - validSince < settings.sharedHoldSeconds) return;

            Gauge.Value = Mathf.Min(settings.gaugeMax, Gauge.Value + settings.gaugeGainPerSecond * Time.deltaTime);
            if (Gauge.Value < settings.gaugeMax) return;
            if (anchor == RaidChainAnchor.FrontLeg) frontLegImmuneUntil = Time.time + settings.partImmunitySeconds;
            else trunkImmuneUntil = Time.time + settings.partImmunitySeconds;
            Gauge.Value = 0f;
            validSince = -1d;
            session.ChainSuccesses.Value++;
            EventSequence.Value++;
            RaidBoss.Instance.ApplyCooperationStagger(settings.bossStaggerSeconds);
            if (anchor == RaidChainAnchor.Trunk) RaidChain.Instance.EndSnatch();
            first.CancelPullOnServer(); second.CancelPullOnServer();
            attemptActive = false;
            Debug.Log("COOP_EVENT: PullAttemptCompleted id=" + EventSequence.Value + " target=" + anchor);
        }

        public void ResetForLobby()
        {
            if (!IsServer) return;
            Gauge.Value = 0f;
            Target.Value = RaidChainAnchor.None;
            FailureReasons.Value = PullFailure.None;
            validSince = -1d;
            attemptActive = false;
            lastLoggedFailure = PullFailure.None;
            frontLegImmuneUntil = trunkImmuneUntil = 0f;
        }
    }
}
