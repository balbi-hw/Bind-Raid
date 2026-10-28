using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    // Server owns every gameplay value. The line is a client-only view of that path.
    [RequireComponent(typeof(NetworkObject), typeof(LineRenderer))]
    public sealed class RaidChain : NetworkBehaviour
    {
        public static RaidChain Instance { get; private set; }

        [Header("Distance tuning")]
        public float warningLength = 7f;
        public float nominalLength = 8f;
        public float hardLength = 9f;
        public float bufferSlowdown = .4f;
        public float pullSpeedLimit = 3f;
        public float staminaCostPerSecond = 8f;
        public float sustainedOverstretchSeconds = 1.5f;
        public float wrapRadius = .65f;
        public float wrapReleaseRadius = 1.25f;

        public readonly NetworkVariable<float> PathLength = new NetworkVariable<float>();
        public readonly NetworkVariable<RaidChainBand> DistanceBand = new NetworkVariable<RaidChainBand>();
        public readonly NetworkVariable<RaidChainState> DisplayState = new NetworkVariable<RaidChainState>();
        public readonly NetworkVariable<RaidChainAnchor> ActiveAnchor = new NetworkVariable<RaidChainAnchor>();
        public readonly NetworkVariable<RaidChainAnchor> SuspendedAnchor = new NetworkVariable<RaidChainAnchor>();
        public readonly NetworkVariable<int> StateEnterTick = new NetworkVariable<int>();

        LineRenderer line;
        float overSince = -1f;
        float attemptUntil;
        bool snatched;
        RaidPlayer clientFirst;
        RaidPlayer clientSecond;
        bool loggedCombatPath;

        void Awake() { Instance = this; line = GetComponent<LineRenderer>(); }
        public override void OnNetworkSpawn()
        {
            Debug.Log("RAID_CHAIN: spawned server=" + IsServer);
        }
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        void Update()
        {
            if (IsServer) TickServer();
        }

        void LateUpdate()
        {
            if (!IsSpawned || line == null) return;
            if (RaidSession.Instance == null || RaidSession.Instance.CurrentState != RaidSessionState.Combat)
            { line.enabled = false; return; }
            if (!TryPlayers(out var first, out var second)) { line.enabled = false; return; }
            line.enabled = true;
            Vector3 a = Waist(first), b = Waist(second);
            Vector3? anchor = AnchorPoint(ActiveAnchor.Value);
            line.positionCount = anchor.HasValue ? 3 : 2;
            line.SetPosition(0, a);
            if (anchor.HasValue) line.SetPosition(1, anchor.Value);
            line.SetPosition(line.positionCount - 1, b);
            Color color = DisplayState.Value switch
            {
                RaidChainState.Snatched => new Color(1f, .15f, .15f),
                RaidChainState.Wrapped => new Color(.7f, .3f, 1f),
                RaidChainState.Overstretched => Color.red,
                RaidChainState.Taut => new Color(1f, .75f, .15f),
                _ => new Color(.65f, .85f, 1f)
            };
            line.startColor = line.endColor = color;
        }

        public Vector3 ConstrainMove(RaidPlayer moving, Vector3 displacement)
        {
            if (!IsServer || !TryPlayers(out var first, out var second) ||
                displacement.sqrMagnitude < .000001f) return displacement;
            Vector3 a = Waist(first), b = Waist(second);
            Vector3? anchor = AnchorPoint(ActiveAnchor.Value);
            Vector3 result = RaidChainRules.ConstrainDisplacement(a, b, anchor, moving == first,
                displacement, nominalLength, hardLength, bufferSlowdown, out bool attemptedHard);
            if (attemptedHard) attemptUntil = Time.time + .2f;
            return result;
        }

        void TickServer()
        {
            if (!TryPlayers(out var first, out var second)) { ResetState(); return; }
            bool combat = RaidSession.Instance != null && RaidSession.Instance.CurrentState == RaidSessionState.Combat;
            if (!combat) { ResetState(); return; }
            if (snatched && !AnchorPoint(RaidChainAnchor.Trunk).HasValue) EndSnatch();
            UpdateWrap(first, second);
            Vector3 a = Waist(first), b = Waist(second);
            float length = RaidChainRules.Length(a, b, AnchorPoint(ActiveAnchor.Value));
            if (length > hardLength || Time.time < attemptUntil)
            {
                if (overSince < 0f) overSince = Time.time;
                PullTowardEachOther(first, second, Time.deltaTime);
                first.DrainChainStamina(staminaCostPerSecond * Time.deltaTime);
                second.DrainChainStamina(staminaCostPerSecond * Time.deltaTime);
                if (Time.time - overSince >= sustainedOverstretchSeconds)
                {
                    first.ChainKnockdown(); second.ChainKnockdown();
                    overSince = Time.time;
                }
            }
            else overSince = -1f;
            PathLength.Value = length;
            if (!loggedCombatPath)
            { loggedCombatPath = true; Debug.Log("RAID_CHAIN: combat path=" + length.ToString("F2")); }
            RaidChainBand band = RaidChainRules.Band(length, warningLength, nominalLength, hardLength);
            if (Time.time < attemptUntil && band != RaidChainBand.Overstretched)
                band = RaidChainBand.Overstretched;
            DistanceBand.Value = band;
            bool down = first.Action.Value == RaidAction.Knockdown || first.IsDown ||
                second.Action.Value == RaidAction.Knockdown || second.IsDown;
            bool grabbed = first.Action.Value == RaidAction.Grabbed || second.Action.Value == RaidAction.Grabbed;
            RaidChainState next = RaidChainRules.Display(band, ActiveAnchor.Value == RaidChainAnchor.FrontLeg,
                snatched, grabbed, down);
            if (next != DisplayState.Value) { DisplayState.Value = next; StateEnterTick.Value = NetworkManager.ServerTime.Tick; }
        }

        void UpdateWrap(RaidPlayer first, RaidPlayer second)
        {
            if (snatched) return;
            Vector3? point = AnchorPoint(RaidChainAnchor.FrontLeg);
            if (!point.HasValue) { ActiveAnchor.Value = RaidChainAnchor.None; return; }
            float distance = WrapDistance(first, second, point.Value, out bool between);
            if (ActiveAnchor.Value == RaidChainAnchor.None && between && distance <= wrapRadius)
                ActiveAnchor.Value = RaidChainAnchor.FrontLeg;
            else if (ActiveAnchor.Value == RaidChainAnchor.FrontLeg && (!between || distance > wrapReleaseRadius))
                ActiveAnchor.Value = RaidChainAnchor.None;
        }

        float WrapDistance(RaidPlayer first, RaidPlayer second, Vector3 point, out bool between)
        {
            Vector2 a = XZ(Waist(first)), b = XZ(Waist(second)), p = XZ(point);
            Vector2 segment = b - a;
            float t = segment.sqrMagnitude > .001f ?
                Mathf.Clamp01(Vector2.Dot(p - a, segment) / segment.sqrMagnitude) : 0f;
            between = t > .1f && t < .9f && Vector2.Distance(a, p) > wrapRadius &&
                Vector2.Distance(b, p) > wrapRadius;
            return Vector2.Distance(p, a + segment * t);
        }

        // Called only by a future boss skill. No boss attack is part of feature 03.
        public bool BeginSnatch()
        {
            if (!IsServer || snatched || RaidSession.Instance == null ||
                RaidSession.Instance.CurrentState != RaidSessionState.Combat ||
                !AnchorPoint(RaidChainAnchor.Trunk).HasValue) return false;
            SuspendedAnchor.Value = ActiveAnchor.Value == RaidChainAnchor.FrontLeg ?
                RaidChainAnchor.FrontLeg : RaidChainAnchor.None;
            ActiveAnchor.Value = RaidChainAnchor.Trunk;
            snatched = true;
            return true;
        }

        public void EndSnatch()
        {
            if (!IsServer || !snatched) return;
            snatched = false;
            Vector3? leg = AnchorPoint(RaidChainAnchor.FrontLeg);
            bool restore = SuspendedAnchor.Value == RaidChainAnchor.FrontLeg && leg.HasValue &&
                TryPlayers(out var first, out var second) &&
                WrapDistance(first, second, leg.Value, out bool between) <= wrapReleaseRadius && between;
            ActiveAnchor.Value = restore ? RaidChainAnchor.FrontLeg : RaidChainAnchor.None;
            SuspendedAnchor.Value = RaidChainAnchor.None;
        }

        public void ResetState()
        {
            if (!IsServer) return;
            snatched = false;
            ActiveAnchor.Value = RaidChainAnchor.None;
            SuspendedAnchor.Value = RaidChainAnchor.None;
            PathLength.Value = 0f;
            DistanceBand.Value = RaidChainBand.Slack;
            DisplayState.Value = RaidChainState.Slack;
            overSince = -1f;
            attemptUntil = 0f;
            loggedCombatPath = false;
            if (line != null) line.enabled = false;
        }

        void PullTowardEachOther(RaidPlayer first, RaidPlayer second, float dt)
        {
            Vector3 a = first.transform.position, b = second.transform.position;
            Vector3? anchor = AnchorPoint(ActiveAnchor.Value);
            Vector3 firstGoal = anchor ?? b;
            Vector3 secondGoal = anchor ?? a;
            float step = pullSpeedLimit * dt;
            if (!first.IsDown && first.Action.Value != RaidAction.Grabbed)
                first.GetComponent<CharacterController>().Move(Toward(a, firstGoal, step));
            if (!second.IsDown && second.Action.Value != RaidAction.Grabbed)
                second.GetComponent<CharacterController>().Move(Toward(b, secondGoal, step));
        }

        static Vector3 Toward(Vector3 from, Vector3 to, float maxStep)
        {
            Vector3 direction = to - from; direction.y = 0f;
            return Vector3.ClampMagnitude(direction, maxStep);
        }

        bool TryPlayers(out RaidPlayer first, out RaidPlayer second)
        {
            first = second = null;
            var session = RaidSession.Instance;
            if (session == null) return false;
            if (IsServer)
            {
                if (session.Players.Count != 2) return false;
                first = session.Players[0]; second = session.Players[1];
            }
            else
            {
                if (clientFirst == null || clientSecond == null || !clientFirst.IsSpawned || !clientSecond.IsSpawned)
                {
                    clientFirst = clientSecond = null;
                    foreach (var player in FindObjectsByType<RaidPlayer>(FindObjectsSortMode.None))
                    {
                        if (!player.IsSpawned) continue;
                        if (clientFirst == null) clientFirst = player;
                        else if (clientSecond == null) clientSecond = player;
                        else return false;
                    }
                }
                first = clientFirst; second = clientSecond;
            }
            return first != null && second != null && first.IsSpawned && second.IsSpawned;
        }

        Vector3? AnchorPoint(RaidChainAnchor anchor)
        {
            var boss = RaidBoss.Instance;
            if (boss == null || !boss.IsSpawned || boss.Health.Value <= 0f) return null;
            if (anchor == RaidChainAnchor.FrontLeg && boss.FrontLegDurability.Value > 0f)
                return boss.transform.TransformPoint(new Vector3(.37f, -.25f, -.32f));
            if (anchor == RaidChainAnchor.Trunk && boss.TrunkDurability.Value > 0f)
                return boss.transform.TransformPoint(new Vector3(0f, .06f, -.58f));
            return null;
        }

        static Vector3 Waist(RaidPlayer player) => player.transform.position + Vector3.up * .15f;
        static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);
    }
}
