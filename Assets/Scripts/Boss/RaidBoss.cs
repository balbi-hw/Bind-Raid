using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class RaidBoss : NetworkBehaviour
    {
        public static RaidBoss Instance { get; private set; }
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
        float nextStomp;
        float staggerUntil;

        void Awake() { Instance = this; }
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
            nextStomp = Time.time + 5f;
            Staggered.Value = false;
        }

        void Update()
        {
            if (!IsServer) return;
            if (RaidSession.Instance == null ||
                RaidSession.Instance.CurrentState != RaidSessionState.Combat || Health.Value <= 0f)
            {
                if (Telegraphing.Value) Telegraphing.Value = false;
                return;
            }
            if (Staggered.Value)
            {
                Telegraphing.Value = false;
                if (Time.time < staggerUntil) return;
                Staggered.Value = false;
            }
            if (Time.time < nextStomp)
            {
                Telegraphing.Value = Time.time >= nextStomp - 1.2f;
                return;
            }
            Telegraphing.Value = false;
            StompSequence.Value++;
            nextStomp = Time.time + 5f;
            foreach (var player in RaidSession.Instance.Players)
                if (player != null && Vector3.Distance(player.transform.position, transform.position) < 4f)
                    player.ReceiveDamage(stompBasePower, attackPower, true);
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
                if (intact && TrunkDurability.Value == 0f) StaggerCount.Value++;
            }
            else if (part == BossPart.FrontLeg)
            {
                bool intact = FrontLegDurability.Value > 0f;
                FrontLegDurability.Value = Mathf.Max(0f, FrontLegDurability.Value - partPower);
                if (intact && FrontLegDurability.Value == 0f) StaggerCount.Value++;
            }
        }

        public void ApplyCooperationStagger(float seconds)
        {
            if (!IsServer || Health.Value <= 0f) return;
            StaggerCount.Value++;
            Staggered.Value = true;
            Telegraphing.Value = false;
            staggerUntil = Time.time + Mathf.Max(.1f, seconds);
            nextStomp = staggerUntil + 2f;
            Debug.Log("COOP_EVENT: StaggerTriggered count=" + StaggerCount.Value);
        }

        public void ResetForLobby()
        {
            if (!IsServer) return;
            Health.Value = maxHealth;
            TrunkDurability.Value = partMaxDurability;
            FrontLegDurability.Value = partMaxDurability;
            StaggerCount.Value = 0;
            Telegraphing.Value = false;
            StompSequence.Value = 0;
            Staggered.Value = false;
            staggerUntil = 0f;
            nextStomp = Time.time + 5f;
        }
    }
}
