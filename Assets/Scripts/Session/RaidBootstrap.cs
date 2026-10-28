using System;
using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ChainboundRaid
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class RaidBootstrap : MonoBehaviour
    {
        NetworkManager manager;
        UnityTransport transport;
        string address = "127.0.0.1";
        string connectionMessage;
        bool autoFlow;
        bool readySent;
        bool tutorialSent;
        bool testWin;
        bool testLose;
        bool testTimeout;
        bool testRevive;
        bool testChain;
        bool chainTestStarted;
        bool reviveTestStarted;
        bool testEndSent;
        bool autoReturn;
        bool returnSent;
        float resultSeenTime = -1f;
        readonly HashSet<ulong> admittedClients = new HashSet<ulong>();

        void Awake()
        {
            manager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            transport.DisconnectTimeoutMS = 5000;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnClientConnectedCallback += Connected;
        }

        void OnDestroy()
        {
            if (manager != null) manager.OnClientDisconnectCallback -= Disconnected;
            if (manager != null) manager.OnClientConnectedCallback -= Connected;
        }

        void Connected(ulong clientId)
        {
            if (manager.IsServer) admittedClients.Add(clientId);
            Debug.Log("RAID_TEST: connected client " + clientId);
        }

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            autoFlow = Array.IndexOf(args, "-raidAutoFlow") >= 0;
            testWin = Array.IndexOf(args, "-raidTestWin") >= 0;
            testLose = Array.IndexOf(args, "-raidTestLose") >= 0;
            testTimeout = Array.IndexOf(args, "-raidTestTimeout") >= 0;
            testRevive = Array.IndexOf(args, "-raidTestRevive") >= 0;
            testChain = Array.IndexOf(args, "-raidTestChain") >= 0;
            autoReturn = Array.IndexOf(args, "-raidAutoReturn") >= 0;
            if (Array.IndexOf(args, "-raidHost") >= 0)
            {
                transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
                Debug.Log("RAID_TEST: host started=" + manager.StartHost());
            }
            else if (Array.IndexOf(args, "-raidClient") >= 0)
            {
                transport.SetConnectionData("127.0.0.1", 7777);
                Debug.Log("RAID_TEST: client started=" + manager.StartClient());
            }
        }

        void Update()
        {
            if (autoReturn && !returnSent && manager.IsConnectedClient && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Result)
            {
                if (resultSeenTime < 0f) resultSeenTime = Time.time;
                if (Time.time - resultSeenTime >= 1f)
                { returnSent = true; RaidSession.Instance.ReturnToLobbyRpc(); }
            }
            if (manager.IsServer && testRevive && !reviveTestStarted && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Combat && RaidSession.Instance.Players.Count == 2)
            { reviveTestStarted = true; StartCoroutine(RunReviveSmoke()); }
            if (manager.IsServer && testChain && !chainTestStarted && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Combat && RaidSession.Instance.Players.Count == 2)
            { chainTestStarted = true; StartCoroutine(RunChainSmoke()); }
            if (manager.IsServer && testTimeout && RaidSession.Instance != null)
                RaidSession.Instance.maxCombatSeconds = 2f;
            if (manager.IsServer && !testEndSent && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Combat &&
                RaidSession.Instance.CombatSeconds.Value >= 2f)
            {
                if (testWin && RaidBoss.Instance != null)
                { testEndSent = true; RaidBoss.Instance.Health.Value = 0f; Debug.Log("RAID_TEST: forced boss defeat"); }
                else if (testLose && RaidSession.Instance.Players.Count == 2)
                {
                    testEndSent = true;
                    foreach (var player in RaidSession.Instance.Players) player.ReceiveDamage(1000f, 1000f);
                    Debug.Log("RAID_TEST: forced both players down");
                }
            }
            if (!autoFlow || !manager.IsConnectedClient || RaidSession.Instance == null ||
                !RaidSession.Instance.IsSpawned) return;
            var state = RaidSession.Instance.CurrentState;
            if (state == RaidSessionState.Lobby && !readySent && manager.LocalClient != null &&
                manager.LocalClient.PlayerObject != null)
            { readySent = true; RaidSession.Instance.SetReadyRpc(true); }
            if (state == RaidSessionState.Tutorial && !tutorialSent)
            { tutorialSent = true; RaidSession.Instance.CompleteTutorialRpc(); }
            if (state == RaidSessionState.Combat && tutorialSent)
            { Debug.Log("RAID_TEST: combat entered"); autoFlow = false; }
        }

        IEnumerator RunReviveSmoke()
        {
            var helper = RaidSession.Instance.Players[0];
            var target = RaidSession.Instance.Players[1];
            helper.GetComponent<CharacterController>().Move(
                target.transform.position + Vector3.right * 1.5f - helper.transform.position);
            for (int down = 1; down <= 2; down++)
            {
                target.ReceiveDamage(1000f, 1000f);
                Debug.Log("RAID_TEST: down=" + target.DownCount.Value +
                    " required=" + target.RequiredReviveSeconds);
                helper.SetTestReviveHold(true);
                yield return new WaitForSeconds(target.RequiredReviveSeconds + 0.5f);
                helper.SetTestReviveHold(false);
                Debug.Log("RAID_TEST: revived=" + !target.IsDown +
                    " count=" + RaidSession.Instance.RevivesUsed.Value);
                if (target.IsDown) yield break;
                yield return null;
            }
        }

        IEnumerator RunChainSmoke()
        {
            var first = RaidSession.Instance.Players[0];
            var second = RaidSession.Instance.Players[1];
            var chain = RaidChain.Instance;
            if (chain == null) { Debug.LogError("CHAIN_SMOKE: missing chain"); yield break; }
            Vector3 leg = RaidBoss.Instance.transform.TransformPoint(new Vector3(.37f, -.25f, -.32f));
            TeleportForTest(first, new Vector3(leg.x, 1f, leg.z - 3f));
            TeleportForTest(second, new Vector3(leg.x, 1f, leg.z + 3f));
            yield return new WaitForSeconds(.25f);
            bool wrapped = chain.ActiveAnchor.Value == RaidChainAnchor.FrontLeg;
            bool snatched = chain.BeginSnatch();
            yield return null;
            bool suspended = snatched && chain.ActiveAnchor.Value == RaidChainAnchor.Trunk &&
                chain.SuspendedAnchor.Value == RaidChainAnchor.FrontLeg;
            chain.EndSnatch();
            yield return null;
            bool restored = chain.ActiveAnchor.Value == RaidChainAnchor.FrontLeg;
            Debug.Log("CHAIN_SMOKE: wrapped=" + wrapped + " suspended=" + suspended +
                " restored=" + restored);
            if (!wrapped || !suspended || !restored) Debug.LogError("CHAIN_SMOKE: anchor transition failed");
            TeleportForTest(first, new Vector3(-6f, 1f, -4f));
            TeleportForTest(second, new Vector3(6f, 1f, -4f));
            yield return null;
            bool over = chain.DistanceBand.Value == RaidChainBand.Overstretched;
            Debug.Log("CHAIN_SMOKE: overstretched=" + over + " length=" + chain.PathLength.Value.ToString("F2"));
            if (!over) Debug.LogError("CHAIN_SMOKE: overstretch state failed");
        }

        static void TeleportForTest(RaidPlayer player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool room = manager.ConnectedClientsIds.Count < 2;
            bool open = RaidSession.Instance == null || RaidSession.Instance.CurrentState == RaidSessionState.Lobby;
            response.Approved = room && open;
            response.CreatePlayerObject = response.Approved;
            response.Position = new Vector3(manager.ConnectedClientsIds.Count == 0 ? -2f : 2f, 1f, -4f);
            response.Rotation = Quaternion.identity;
            if (!room) response.Reason = "The two-player room is full.";
            else if (!open) response.Reason = "The raid has already started.";
        }

        void Disconnected(ulong clientId)
        {
            bool admitted = admittedClients.Remove(clientId);
            if (manager.IsServer && admitted && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState != RaidSessionState.Lobby &&
                RaidSession.Instance.CurrentState != RaidSessionState.Result)
                RaidSession.Instance.ConnectionLost();
            if (!manager.IsServer && clientId == manager.LocalClientId)
            {
                connectionMessage = string.IsNullOrEmpty(manager.DisconnectReason) ?
                    "Connection lost. Return to the lobby." : manager.DisconnectReason;
                Debug.Log("RAID_TEST: disconnected reason=" + connectionMessage);
            }
        }

        void OnGUI()
        {
            GUI.skin.label.fontSize = 15;
            GUI.skin.button.fontSize = 15;
            GUI.skin.button.fixedHeight = 30;
            GUI.skin.textField.fontSize = 15;
            GUI.skin.textField.fixedHeight = 28;
            GUILayout.BeginArea(new Rect(12, 12, 470, 455), GUI.skin.box);
            GUILayout.Label("CHAINBOUND RAID | Developer Session");
            if (!manager.IsListening)
            {
                GUILayout.Label("Host address:");
                address = GUILayout.TextField(address);
                if (GUILayout.Button("Host (port 7777)"))
                { transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0"); manager.StartHost(); connectionMessage = ""; }
                if (GUILayout.Button("Join"))
                { transport.SetConnectionData(address, 7777); manager.StartClient(); connectionMessage = ""; }
                GUILayout.Label(connectionMessage);
                GUILayout.EndArea(); return;
            }
            var session = RaidSession.Instance;
            if (session == null || !session.IsSpawned)
            { GUILayout.Label("Connecting..."); GUILayout.EndArea(); return; }
            GUILayout.Label("Players: " + manager.ConnectedClientsIds.Count + "/2");
            GUILayout.Label("State: " + session.CurrentState);
            if (session.CurrentState == RaidSessionState.Lobby)
            {
                GUILayout.Label("Ready: " + session.FirstReady.Value + " / " + session.SecondReady.Value);
                if (GUILayout.Button("Toggle ready"))
                {
                    bool ready = manager.LocalClientId == 0 ? session.FirstReady.Value : session.SecondReady.Value;
                    session.SetReadyRpc(!ready);
                }
            }
            else if (session.CurrentState == RaidSessionState.Tutorial)
            {
                GUILayout.Label("Tutorial objectives are pending feature 08.");
                if (GUILayout.Button("Developer: complete tutorial")) session.CompleteTutorialRpc();
            }
            else if (session.CurrentState == RaidSessionState.Combat)
            {
                GUILayout.Label("Time: " + session.CombatSeconds.Value.ToString("F0") + " / 1080 s");
                if (RaidBoss.Instance != null)
                {
                    GUILayout.Label("Boss HP: " + RaidBoss.Instance.Health.Value.ToString("F0"));
                    GUILayout.Label("Trunk / leg durability: " + RaidBoss.Instance.TrunkDurability.Value.ToString("F0") +
                        " / " + RaidBoss.Instance.FrontLegDurability.Value.ToString("F0"));
                }
                var local = manager.LocalClient != null && manager.LocalClient.PlayerObject != null ?
                    manager.LocalClient.PlayerObject.GetComponent<RaidPlayer>() : null;
                if (local != null)
                {
                    GUILayout.Label("HP " + local.Health.Value.ToString("F0") + " | Stamina " + local.Stamina.Value.ToString("F0"));
                    GUILayout.Label("Action " + local.Action.Value + " | Target " +
                        (local.LockEnabled.Value ? local.LockedPart.Value.ToString() : "None"));
                    if (local.IsDown)
                        GUILayout.Label("Revive " + local.ReviveProgress.Value.ToString("F1") + " / " +
                            local.RequiredReviveSeconds.ToString("F0") + " s");
                }
                foreach (var player in FindObjectsByType<RaidPlayer>(FindObjectsSortMode.None))
                {
                    if (local == null || player == local || !player.IsSpawned) continue;
                    GUILayout.Label("Partner HP " + player.Health.Value.ToString("F0") +
                        " | Distance " + Vector3.Distance(local.transform.position, player.transform.position).ToString("F1") + " m");
                }
                if (RaidChain.Instance != null && RaidChain.Instance.IsSpawned)
                    GUILayout.Label("Chain " + RaidChain.Instance.PathLength.Value.ToString("F1") + " m | " +
                        RaidChain.Instance.DistanceBand.Value + " | " + RaidChain.Instance.DisplayState.Value +
                        " | Anchor " + RaidChain.Instance.ActiveAnchor.Value);
                GUILayout.Label("WASD move | Mouse look | Shift sprint | Space dodge");
                GUILayout.Label("Left/Right click attacks | Tab target part | E revive");
            }
            else if (session.CurrentState == RaidSessionState.Result)
            {
                GUILayout.Label("Result: " + session.CurrentResult);
                GUILayout.Label("Time " + session.CombatSeconds.Value.ToString("F0") + " s | Revives " + session.RevivesUsed.Value);
                GUILayout.Label("Staggers " + session.StaggerCount.Value + " | Chain " +
                    session.ChainSuccesses.Value + "/" + session.ChainAttempts.Value);
                if (GUILayout.Button("Return to lobby")) session.ReturnToLobbyRpc();
            }
            GUILayout.EndArea();
        }
    }
}
