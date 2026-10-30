using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public enum RaidSessionState { Lobby, Tutorial, Combat, Result, Disconnected }
    public enum RaidResultReason { None, BossDefeated, BothPlayersDown, TimeLimit, ConnectionLost }
    public enum RaidPing { Approach, Pull, Release, Rescue }

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
        public readonly NetworkVariable<ulong> FirstPlayerClientId = new NetworkVariable<ulong>(ulong.MaxValue);
        public readonly NetworkVariable<bool> FirstTutorialDone = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> SecondTutorialDone = new NetworkVariable<bool>();
        public readonly NetworkVariable<int> TutorialStep = new NetworkVariable<int>();
        public readonly NetworkVariable<int> TutorialProgress = new NetworkVariable<int>();
        public readonly NetworkVariable<float> CombatSeconds = new NetworkVariable<float>();
        public readonly NetworkVariable<int> RevivesUsed = new NetworkVariable<int>();
        public readonly NetworkVariable<int> ChainAttempts = new NetworkVariable<int>();
        public readonly NetworkVariable<int> ChainSuccesses = new NetworkVariable<int>();
        public readonly NetworkVariable<int> StaggerCount = new NetworkVariable<int>();
        public readonly NetworkVariable<int> PingSequence = new NetworkVariable<int>();
        public readonly NetworkVariable<RaidPing> PingKind = new NetworkVariable<RaidPing>();
        public readonly NetworkVariable<ulong> PingSender = new NetworkVariable<ulong>();
        public readonly NetworkVariable<int> PingServerTick = new NetworkVariable<int>();
        public readonly NetworkVariable<double> PingServerTime = new NetworkVariable<double>();
        readonly float[,] lastPingAt = new float[2, 4];
        public float maxCombatSeconds = 18f * 60f;
        public float pingRepeatCooldown = 1.5f;
        public float pingDisplaySeconds = 3f;
        public readonly List<RaidPlayer> Players = new List<RaidPlayer>(2);
        float combatStartTime;
        int tutorialFlags;
        int tutorialBaseline;
        int tutorialStaggerBaseline;
        bool tutorialAttackStarted;

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
            if (CurrentState == RaidSessionState.Tutorial)
            { TickTutorial(); return; }
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

        void TickTutorial()
        {
            if (Players.Count != 2 || RaidChain.Instance == null || RaidBoss.Instance == null) return;
            var chain = RaidChain.Instance;
            var boss = RaidBoss.Instance;
            switch (TutorialStep.Value)
            {
                case 0:
                    if (chain.DistanceBand.Value != RaidChainBand.Slack) tutorialFlags |= 1;
                    if ((tutorialFlags & 1) != 0 && chain.DistanceBand.Value == RaidChainBand.Slack)
                        AdvanceTutorial();
                    break;
                case 1:
                    for (int i = 0; i < 2; i++)
                    {
                        if (Players[i].Stance.Value == ChainStance.Braced) tutorialFlags |= 1 << i;
                        if ((tutorialFlags & (1 << i)) != 0 && Players[i].Stance.Value == ChainStance.Flexible)
                            tutorialFlags |= 1 << (i + 2);
                    }
                    TutorialProgress.Value = tutorialFlags;
                    if ((tutorialFlags & 15) == 15) AdvanceTutorial();
                    break;
                case 2:
                    if (ChainSuccesses.Value > tutorialBaseline) AdvanceTutorial();
                    break;
                case 3:
                    if (!tutorialAttackStarted && boss.Stage.Value == BossAttackStage.Idle &&
                        chain.PathLength.Value >= chain.warningLength)
                    { tutorialAttackStarted = true; boss.ForceTutorialAttack(BossAttack.ChainSnatch, Players[1]); }
                    if (boss.SnatchAvoidCount > tutorialBaseline || boss.StaggerCount.Value > tutorialStaggerBaseline)
                        AdvanceTutorial();
                    else if (tutorialAttackStarted && boss.Stage.Value == BossAttackStage.Recovery)
                        tutorialAttackStarted = false;
                    break;
                case 4:
                    if (!tutorialAttackStarted && boss.Stage.Value == BossAttackStage.Idle &&
                        Vector3.Distance(Players[1].transform.position, boss.transform.position) <= boss.settings.spinGrabRange)
                    { tutorialAttackStarted = true; boss.ForceTutorialAttack(BossAttack.SpinGrab, Players[1]); }
                    if (boss.SpinRescueCount > tutorialBaseline) AdvanceTutorial();
                    else if (tutorialAttackStarted && boss.Stage.Value == BossAttackStage.Recovery)
                        tutorialAttackStarted = false;
                    break;
            }
            // Practice damage never ends the hunt; restore players between attempts.
            for (int i = 0; i < 2; i++)
                if (Players[i].IsDown) Players[i].ResetForTutorial();
        }

        void AdvanceTutorial()
        {
            int next = TutorialStep.Value + 1;
            Debug.Log("TUTORIAL_EVENT: completed step=" + TutorialStep.Value);
            tutorialFlags = 0;
            tutorialAttackStarted = false;
            TutorialProgress.Value = 0;
            if (next >= 5)
            {
                foreach (var player in Players) player.ResetForTutorial();
                RaidBoss.Instance.ResetForLobby();
                RaidChain.Instance.ResetState();
                RaidCooperation.Instance.ResetForLobby();
                FirstTutorialDone.Value = SecondTutorialDone.Value = true;
                SetStatus(RaidSessionState.Combat, RaidResultReason.None);
                combatStartTime = Time.time;
                Debug.Log("TUTORIAL_EVENT: combat unlocked");
                return;
            }
            TutorialStep.Value = next;
            tutorialBaseline = next switch
            {
                2 => ChainSuccesses.Value,
                3 => RaidBoss.Instance.SnatchAvoidCount,
                4 => RaidBoss.Instance.SpinRescueCount,
                _ => 0
            };
            if (next == 3) tutorialStaggerBaseline = RaidBoss.Instance.StaggerCount.Value;
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
            FirstPlayerClientId.Value = Players.Count > 0 ? Players[0].OwnerClientId : ulong.MaxValue;
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
            {
                TutorialStep.Value = 0; TutorialProgress.Value = 0;
                tutorialFlags = 0; tutorialAttackStarted = false;
                SetStatus(RaidSessionState.Tutorial, RaidResultReason.None);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CompleteTutorialRpc(RpcParams rpcParams = default)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#endif
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SendPingRpc(RaidPing kind, RpcParams rpcParams = default)
        {
            if (CurrentState != RaidSessionState.Combat || (int)kind < 0 || (int)kind > 3) return;
            int slot = SlotOf(rpcParams.Receive.SenderClientId);
            if (slot < 0) return;
            if (Time.time - lastPingAt[slot, (int)kind] < pingRepeatCooldown) return;
            lastPingAt[slot, (int)kind] = Time.time;
            PingKind.Value = kind;
            PingSender.Value = rpcParams.Receive.SenderClientId;
            PingServerTick.Value = NetworkManager.ServerTime.Tick;
            PingServerTime.Value = NetworkManager.ServerTime.Time;
            PingSequence.Value++;
            Debug.Log("HUD_EVENT: Ping kind=" + kind + " sender=" + PingSender.Value +
                " tick=" + PingServerTick.Value);
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
            TutorialStep.Value = TutorialProgress.Value = 0;
            CombatSeconds.Value = 0f;
            RevivesUsed.Value = 0;
            if (RaidBoss.Instance != null) RaidBoss.Instance.ResetForLobby();
            foreach (var player in Players) player.ResetForLobby();
        }
    }
}
