using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public enum RaidSessionState { Lobby, Tutorial, Combat, Result, Disconnected }
    public enum RaidResultReason { None, BossDefeated, BothPlayersDown, TimeLimit, ConnectionLost }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class RaidSession : NetworkBehaviour
    {
        public static RaidSession Instance { get; private set; }
        // Phase and result share one network value so clients never observe Result with a stale reason.
        public readonly NetworkVariable<int> Status = new NetworkVariable<int>();
        public RaidSessionState CurrentState => (RaidSessionState)(Status.Value & 0xff);
        public RaidResultReason CurrentResult => (RaidResultReason)((Status.Value >> 8) & 0xff);
        public readonly NetworkVariable<bool> FirstReady = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> SecondReady = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> FirstTutorialDone = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> SecondTutorialDone = new NetworkVariable<bool>();
        public readonly NetworkVariable<float> CombatSeconds = new NetworkVariable<float>();
        public readonly NetworkVariable<int> RevivesUsed = new NetworkVariable<int>();
        public readonly NetworkVariable<int> ChainAttempts = new NetworkVariable<int>();
        public readonly NetworkVariable<int> ChainSuccesses = new NetworkVariable<int>();
        public readonly NetworkVariable<int> StaggerCount = new NetworkVariable<int>();
        public float maxCombatSeconds = 18f * 60f;
        public readonly List<RaidPlayer> Players = new List<RaidPlayer>(2);
        float combatStartTime;

        void Awake() { Instance = this; }
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log("RAID_TEST: session spawned server=" + IsServer + " client=" + IsClient);
            if (IsServer) SetStatus(RaidSessionState.Lobby, RaidResultReason.None);
            Status.OnValueChanged += StateChanged;
        }

        public override void OnNetworkDespawn()
        {
            Status.OnValueChanged -= StateChanged;
            base.OnNetworkDespawn();
        }

        void StateChanged(int previous, int current)
        {
            Debug.Log("RAID_SESSION: " + (RaidSessionState)(previous & 0xff) + " -> " + CurrentState +
                (CurrentState == RaidSessionState.Result ? " reason=" + CurrentResult : ""));
        }

        void SetStatus(RaidSessionState state, RaidResultReason reason)
        {
            Status.Value = (int)state | ((int)reason << 8);
        }

        void Update()
        {
            if (!IsServer) return;
            RefreshPlayers();
            if (CurrentState != RaidSessionState.Combat) return;
            CombatSeconds.Value = Time.time - combatStartTime;
            if (RaidBoss.Instance != null) StaggerCount.Value = RaidBoss.Instance.StaggerCount.Value;
            // The boss win condition wins ties on the same authoritative update.
            if (RaidBoss.Instance != null && RaidBoss.Instance.Health.Value <= 0f)
            { Finish(RaidResultReason.BossDefeated); return; }
            if (Players.Count == 2 && Players[0].IsDown && Players[1].IsDown)
            { Finish(RaidResultReason.BothPlayersDown); return; }
            if (CombatSeconds.Value >= maxCombatSeconds)
            { Finish(RaidResultReason.TimeLimit); return; }

            if (Players.Count != 2) return;
            for (int i = 0; i < 2; i++)
            {
                var target = Players[i];
                var helper = Players[1 - i];
                bool valid = helper.Action.Value == RaidAction.Free && helper.ReviveIntent &&
                    Vector3.Distance(helper.transform.position, target.transform.position) <= 2.5f;
                bool wasDown = target.IsDown;
                bool hadProgress = target.ReviveProgress.Value > 0f;
                if (wasDown && valid && !hadProgress)
                    Debug.Log("COOP_EVENT: RescueStarted target=" + target.OwnerClientId);
                if (wasDown && !valid && hadProgress)
                    Debug.Log("COOP_EVENT: RescueCancelled target=" + target.OwnerClientId);
                target.TickRevive(Time.deltaTime, valid);
                if (wasDown && !target.IsDown)
                {
                    RevivesUsed.Value++;
                    Debug.Log("COOP_EVENT: RescueCompleted target=" + target.OwnerClientId);
                }
            }
        }

        void RefreshPlayers()
        {
            Players.Clear();
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var player = client.PlayerObject.GetComponent<RaidPlayer>();
                if (player != null) Players.Add(player);
            }
            Players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        }

        int SlotOf(ulong clientId)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].OwnerClientId == clientId) return i;
            return -1;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            if (CurrentState != RaidSessionState.Lobby) return;
            RefreshPlayers();
            int slot = SlotOf(rpcParams.Receive.SenderClientId);
            if (slot == 0) FirstReady.Value = ready;
            if (slot == 1) SecondReady.Value = ready;
            if (Players.Count == 2 && FirstReady.Value && SecondReady.Value)
                SetStatus(RaidSessionState.Tutorial, RaidResultReason.None);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CompleteTutorialRpc(RpcParams rpcParams = default)
        {
            if (CurrentState != RaidSessionState.Tutorial) return;
            RefreshPlayers();
            int slot = SlotOf(rpcParams.Receive.SenderClientId);
            if (slot == 0) FirstTutorialDone.Value = true;
            if (slot == 1) SecondTutorialDone.Value = true;
            if (Players.Count == 2 && FirstTutorialDone.Value && SecondTutorialDone.Value)
            { SetStatus(RaidSessionState.Combat, RaidResultReason.None); combatStartTime = Time.time; }
        }

        public void ConnectionLost()
        {
            if (!IsServer || CurrentState == RaidSessionState.Result) return;
            Finish(RaidResultReason.ConnectionLost);
        }

        void Finish(RaidResultReason reason)
        {
            if (CurrentState == RaidSessionState.Result) return;
            SetStatus(RaidSessionState.Result, reason);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ReturnToLobbyRpc(RpcParams rpcParams = default)
        {
            if (CurrentState != RaidSessionState.Result || SlotOf(rpcParams.Receive.SenderClientId) < 0) return;
            SetStatus(RaidSessionState.Lobby, RaidResultReason.None);
            FirstReady.Value = SecondReady.Value = false;
            FirstTutorialDone.Value = SecondTutorialDone.Value = false;
            CombatSeconds.Value = 0f;
            RevivesUsed.Value = 0;
            if (RaidBoss.Instance != null) RaidBoss.Instance.ResetForLobby();
            foreach (var player in Players) player.ResetForLobby();
        }
    }
}
