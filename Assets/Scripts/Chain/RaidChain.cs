using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace ChainboundRaid
{
    // Server owns every gameplay value. The line is a client-only view of that path.
    [DefaultExecutionOrder(-50)]
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
        public float forceRampPerSecond = 6f;
        public float visualFollowSharpness = 20f;
        public float slackSag = .2f;
        public readonly NetworkVariable<float> Tension = new NetworkVariable<float>();

        [Header("Visual chain links")]
        public GameObject chainLinkPrefab;
        public Material chainMaterial;
        public float linkSpacing = .27f;

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
        bool loggedVisualLinks;
        readonly List<GameObject> visualLinks = new List<GameObject>();
        readonly List<Renderer[]> visualRenderers = new List<Renderer[]>();
        MaterialPropertyBlock linkColorBlock;

        void Awake() { Instance = this; line = GetComponent<LineRenderer>(); }
        public override void OnNetworkSpawn()
        {
            Debug.Log("RAID_CHAIN: spawned server=" + IsServer);
            if (IsClient && GetComponent<RaidChainFeedback>() == null) gameObject.AddComponent<RaidChainFeedback>();
        }
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        void FixedUpdate()
        {
            if (IsServer) TickServer();
        }

        void LateUpdate()
        {
            if (!IsSpawned || line == null) return;
            if (!IsClient) { line.enabled = false; return; }
            if (RaidSession.Instance == null ||
                (RaidSession.Instance.CurrentState != RaidSessionState.Combat &&
                 RaidSession.Instance.CurrentState != RaidSessionState.Tutorial))
            { line.enabled = false; HideVisualLinks(); return; }
            if (!TryPlayers(out var first, out var second))
            { line.enabled = false; HideVisualLinks(); return; }
            Vector3 a = Waist(first) + first.PresentationOffset, b = Waist(second) + second.PresentationOffset;
            Vector3? anchor = AnchorPoint(ActiveAnchor.Value);
            Color color = DisplayState.Value switch
            {
                RaidChainState.Snatched => new Color(1f, .15f, .15f),
                RaidChainState.Wrapped => new Color(.7f, .3f, 1f),
                RaidChainState.Overstretched => Color.red,
                RaidChainState.Taut => new Color(1f, .75f, .15f),
                _ => new Color(.65f, .85f, 1f)
            };
            if (RaidBoss.Instance != null && RaidBoss.Instance.Stage.Value == BossAttackStage.Telegraph &&
                RaidBoss.Instance.CurrentAttack.Value == BossAttack.ChainSnatch)
                color = Color.red;
            if (chainLinkPrefab != null && chainMaterial != null)
            {
                line.enabled = false;
                RenderLinks(a, anchor, b, color);
            }
            else
            {
                HideVisualLinks();
                line.enabled = true;
                line.positionCount = anchor.HasValue ? 3 : 2;
                line.SetPosition(0, a);
                if (anchor.HasValue) line.SetPosition(1, anchor.Value);
                line.SetPosition(line.positionCount - 1, b);
                line.startColor = line.endColor = color;
            }
        }

        void RenderLinks(Vector3 a, Vector3? anchor, Vector3 b, Color color)
        {
            int used = 0;
            if (anchor.HasValue)
            {
                used = RenderSegment(a, anchor.Value, used, color);
                used = RenderSegment(anchor.Value, b, used, color);
            }
            else used = RenderSegment(a, b, used, color);
            for (int i = used; i < visualLinks.Count; i++) visualLinks[i].SetActive(false);
            if (!loggedVisualLinks)
            {
                loggedVisualLinks = true;
                Debug.Log("CHAIN_VISUAL: 3D links active=" + used);
            }
        }

        int RenderSegment(Vector3 start, Vector3 end, int index, Color color)
        {
            if (index >= 64) return index;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < .01f) return index;
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(.1f, linkSpacing)), 1, 64 - index);
            Quaternion facing = Quaternion.LookRotation(delta);
            if (linkColorBlock == null) linkColorBlock = new MaterialPropertyBlock();
            linkColorBlock.SetColor("_BaseColor", color);
            for (int i = 0; i < count; i++)
            {
                EnsureVisualLink(index);
                var holder = visualLinks[index];
                bool wasVisible = holder.activeSelf;
                holder.SetActive(true);
                float t = (i + .5f) / count;
                Vector3 desired = Vector3.Lerp(start, end, t) - Vector3.up * slackSag * (1f - Tension.Value) * Mathf.Sin(t * Mathf.PI);
                holder.transform.position = wasVisible ? Vector3.Lerp(holder.transform.position, desired,
                    1f - Mathf.Exp(-visualFollowSharpness * Time.deltaTime)) : desired;
                holder.transform.rotation = facing * Quaternion.Euler(0f, 0f, index % 2 == 0 ? 0f : 90f);
                foreach (var renderer in visualRenderers[index]) renderer.SetPropertyBlock(linkColorBlock);
                index++;
            }
            return index;
        }

        void EnsureVisualLink(int index)
        {
            while (visualLinks.Count <= index)
            {
                var holder = new GameObject("Visual Chain Link " + visualLinks.Count.ToString("00"));
                holder.transform.SetParent(transform, false);
                var model = Instantiate(chainLinkPrefab, holder.transform, false);
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers) renderer.sharedMaterial = chainMaterial;
                holder.SetActive(false);
                visualLinks.Add(holder);
                visualRenderers.Add(renderers);
            }
        }

        void HideVisualLinks()
        {
            foreach (var link in visualLinks) link.SetActive(false);
        }

        public Vector3 ConstrainMove(RaidPlayer moving, Vector3 displacement)
        {
            if (!TryPlayers(out var first, out var second) ||
                displacement.sqrMagnitude < .000001f) return displacement;
            Vector3 a = Waist(first), b = Waist(second);
            Vector3? anchor = AnchorPoint(ActiveAnchor.Value);
            Vector3 result = RaidChainRules.ConstrainDisplacement(a, b, anchor, moving == first,
                displacement, nominalLength, hardLength, bufferSlowdown, out bool attemptedHard);
            if (IsServer && attemptedHard) attemptUntil = Time.time + .2f;
            return result;
        }

        void TickServer()
        {
            if (!TryPlayers(out var first, out var second)) { ResetState(); return; }
            bool combat = RaidSession.Instance != null &&
                (RaidSession.Instance.CurrentState == RaidSessionState.Combat ||
                 RaidSession.Instance.CurrentState == RaidSessionState.Tutorial);
            if (!combat) { ResetState(); return; }
            if (snatched && !AnchorPoint(RaidChainAnchor.Trunk).HasValue) EndSnatch();
            UpdateWrap(first, second);
            Vector3 a = Waist(first), b = Waist(second);
            float length = RaidChainRules.Length(a, b, AnchorPoint(ActiveAnchor.Value));
            if (length > hardLength || Time.time < attemptUntil)
            {
                if (overSince < 0f) overSince = Time.time;
                first.DrainChainStamina(staminaCostPerSecond * Time.fixedDeltaTime);
                second.DrainChainStamina(staminaCostPerSecond * Time.fixedDeltaTime);
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
            float tension = band == RaidChainBand.Overstretched ? 1f : Mathf.InverseLerp(nominalLength, hardLength, length);
            Tension.Value = Mathf.MoveTowards(Tension.Value, tension, forceRampPerSecond * Time.fixedDeltaTime);
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
                (RaidSession.Instance.CurrentState != RaidSessionState.Combat &&
                 RaidSession.Instance.CurrentState != RaidSessionState.Tutorial) ||
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
            Tension.Value = 0f;
            DistanceBand.Value = RaidChainBand.Slack;
            DisplayState.Value = RaidChainState.Slack;
            overSince = -1f;
            attemptUntil = 0f;
            loggedCombatPath = false;
            if (line != null) line.enabled = false;
        }

        // Pure displacement request. The player motor is the only controller writer.
        public Vector3 PullDisplacement(RaidPlayer player, float dt)
        {
            if (DistanceBand.Value != RaidChainBand.Overstretched || player.IsDown ||
                player.Action.Value == RaidAction.Grabbed || !TryPlayers(out var first, out var second)) return Vector3.zero;
            var partner = player == first ? second : first;
            if (player != first && player != second) return Vector3.zero;
            Vector3 goal = AnchorPoint(ActiveAnchor.Value) ?? partner.transform.position;
            return Toward(player.transform.position, goal, pullSpeedLimit * Tension.Value * dt);
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
                    foreach (var player in RaidPlayerRegistry.Active)
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
            if (boss.TryGetChainAnchor(anchor, out var point)) return point;
            return null;
        }

        static Vector3 Waist(RaidPlayer player) => player.ChainAttachmentPosition;
        static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);
    }
}
