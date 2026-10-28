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
                    stompRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.3f, -0.49f,
                        Mathf.Sin(angle) * 1.3f));
                }
            }
        }

        void LateUpdate()
        {
            if (boss == null || !boss.IsSpawned) return;
            bool warning = boss.Telegraphing.Value;
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
        }

        void SetColor(Renderer target, Color color)
        {
            if (target == null) return;
            colorBlock.SetColor("_BaseColor", color);
            target.SetPropertyBlock(colorBlock);
        }
    }
}
