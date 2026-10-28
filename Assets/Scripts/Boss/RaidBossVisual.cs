using UnityEngine;

namespace ChainboundRaid
{
    [RequireComponent(typeof(RaidBoss))]
    public sealed class RaidBossVisual : MonoBehaviour
    {
        public Transform bodyRoot;
        public Renderer body;
        public Renderer trunk;
        public Renderer frontLeg;
        public LineRenderer stompRing;
        public LineRenderer attackTelegraph;
        public LineRenderer rescueRing;

        RaidBoss boss;
        MaterialPropertyBlock colorBlock;
        int previousStomp;
        float stompFlash;

        void Awake()
        {
            boss = GetComponent<RaidBoss>();
            colorBlock = new MaterialPropertyBlock();
            if (stompRing != null)
            {
                stompRing.useWorldSpace = false;
                stompRing.loop = true;
                stompRing.positionCount = 40;
                for (int i = 0; i < 40; i++)
                {
                    float angle = i * Mathf.PI * 2f / 40f;
                    float radius = boss.settings != null ? boss.settings.stompRadius /
                        Mathf.Abs(transform.lossyScale.x) : 1.3f;
                    stompRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, -0.49f,
                        Mathf.Sin(angle) * radius));
                }
            }
        }

        void LateUpdate()
        {
            if (boss == null || !boss.IsSpawned) return;
            bool warning = boss.Stage.Value == BossAttackStage.Telegraph &&
                boss.CurrentAttack.Value == BossAttack.Stomp;
            bool staggered = boss.Staggered.Value;
            if (boss.StompSequence.Value != previousStomp)
            { previousStomp = boss.StompSequence.Value; stompFlash = 0.25f; }
            stompFlash = Mathf.Max(0f, stompFlash - Time.deltaTime);
            if (bodyRoot != null)
            {
                float bob = warning ? 0.035f * Mathf.Sin(Time.time * 13f) : 0.01f * Mathf.Sin(Time.time * 2f);
                bodyRoot.localPosition = new Vector3(0f, bob, 0f);
                bodyRoot.localRotation = Quaternion.Euler(0f, 0f,
                    staggered ? 12f * Mathf.Sin(Time.time * 7f) :
                    warning ? 2.5f * Mathf.Sin(Time.time * 12f) : 0f);
            }
            Color bodyColor = staggered ? new Color(.35f, .85f, 1f) : stompFlash > 0f ? Color.white : warning ?
                new Color(1f, 0.25f, 0.12f) : new Color(0.46f, 0.31f, 0.5f);
            SetColor(body, bodyColor);
            SetColor(trunk, boss.TrunkDurability.Value > 0f ? new Color(0.95f, 0.63f, 0.22f) : Color.gray);
            SetColor(frontLeg, boss.FrontLegDurability.Value > 0f ? new Color(0.9f, 0.67f, 0.24f) : Color.gray);
            if (stompRing != null)
            {
                stompRing.enabled = warning || stompFlash > 0f;
                Color ringColor = stompFlash > 0f ? Color.white : new Color(1f, 0.14f, 0.08f);
                stompRing.startColor = stompRing.endColor = ringColor;
                stompRing.widthMultiplier = warning ? 0.09f + 0.03f * Mathf.Sin(Time.time * 18f) : 0.16f;
            }
            DrawAttackTelegraph();
            DrawRescueMarker();
        }

        void DrawRescueMarker()
        {
            if (rescueRing == null || boss.settings == null) return;
            rescueRing.enabled = boss.Stage.Value == BossAttackStage.Active &&
                boss.CurrentAttack.Value == BossAttack.SpinGrab;
            if (!rescueRing.enabled) return;
            rescueRing.useWorldSpace = true;
            rescueRing.loop = true;
            rescueRing.positionCount = 32;
            rescueRing.startColor = rescueRing.endColor = Color.green;
            Vector3 center = boss.RescuePoint; center.y = .06f;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                rescueRing.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f,
                    Mathf.Sin(angle)) * boss.settings.spinRescueRadius);
            }
        }

        void DrawAttackTelegraph()
        {
            if (attackTelegraph == null || boss.settings == null) return;
            BossAttack attack = boss.CurrentAttack.Value;
            bool visible = boss.Stage.Value == BossAttackStage.Telegraph ||
                (attack == BossAttack.Charge && boss.Stage.Value == BossAttackStage.Active);
            attackTelegraph.enabled = visible && attack != BossAttack.Stomp && attack != BossAttack.None;
            if (!attackTelegraph.enabled) return;
            attackTelegraph.useWorldSpace = true;
            attackTelegraph.startColor = attackTelegraph.endColor = new Color(1f, .2f, .1f);
            Vector3 origin = boss.AttackOrigin.Value;
            origin.y = .06f;
            Vector3 direction = boss.AttackDirection.Value;
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
            direction.Normalize();
            if (attack == BossAttack.Charge)
            {
                Vector3 side = Vector3.Cross(Vector3.up, direction) * boss.settings.chargeWidth;
                Vector3 end = origin + direction * boss.settings.chargeDistance;
                attackTelegraph.loop = true;
                attackTelegraph.positionCount = 4;
                attackTelegraph.SetPosition(0, origin + side);
                attackTelegraph.SetPosition(1, end + side);
                attackTelegraph.SetPosition(2, end - side);
                attackTelegraph.SetPosition(3, origin - side);
            }
            else if (attack == BossAttack.TrunkSweep)
            {
                attackTelegraph.loop = true;
                attackTelegraph.positionCount = 26;
                attackTelegraph.SetPosition(0, origin);
                for (int i = 0; i < 25; i++)
                {
                    float angle = Mathf.Lerp(-boss.settings.sweepHalfAngle,
                        boss.settings.sweepHalfAngle, i / 24f);
                    attackTelegraph.SetPosition(i + 1, origin +
                        Quaternion.Euler(0f, angle, 0f) * direction * boss.settings.sweepRadius);
                }
            }
            else if (attack == BossAttack.SpinGrab)
            {
                Vector3 center = origin;
                foreach (var player in FindObjectsByType<RaidPlayer>(FindObjectsSortMode.None))
                    if (player.IsSpawned && player.OwnerClientId == boss.TargetClientId.Value)
                    { center = player.transform.position; center.y = .06f; break; }
                attackTelegraph.loop = true;
                attackTelegraph.positionCount = 32;
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI * 2f / 32f;
                    attackTelegraph.SetPosition(i, center +
                        new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f);
                }
            }
            else
            {
                Vector3 midpoint = origin;
                var players = FindObjectsByType<RaidPlayer>(FindObjectsSortMode.None);
                if (players.Length >= 2) midpoint = (players[0].transform.position + players[1].transform.position) * .5f;
                midpoint.y = .06f;
                attackTelegraph.loop = false;
                attackTelegraph.positionCount = 2;
                attackTelegraph.SetPosition(0, origin);
                attackTelegraph.SetPosition(1, midpoint);
            }
        }

        void SetColor(Renderer target, Color color)
        {
            if (target == null) return;
            colorBlock.SetColor("_BaseColor", color);
            target.SetPropertyBlock(colorBlock);
        }
    }
}
