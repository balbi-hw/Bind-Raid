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
        bool autoReady;
        bool testTutorial;
        bool tutorialTestStarted;
        bool readySent;
        bool tutorialSent;
        bool testWin;
        bool testLose;
        bool testTimeout;
        bool testRevive;
        bool testChain;
        bool chainTestStarted;
        bool testCooperation;
        bool cooperationTestStarted;
        bool testBoss;
        bool bossTestStarted;
        bool testPing;
        int pingTestIndex;
        float nextPingTestTime;
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
            bool dedicatedServer = Array.IndexOf(args, "-raidServer") >= 0;
#if UNITY_SERVER
            dedicatedServer = true;
#endif
            autoFlow = Array.IndexOf(args, "-raidAutoFlow") >= 0;
            autoReady = Array.IndexOf(args, "-raidAutoReady") >= 0;
            testTutorial = Array.IndexOf(args, "-raidTestTutorial") >= 0;
            testWin = Array.IndexOf(args, "-raidTestWin") >= 0;
            testLose = Array.IndexOf(args, "-raidTestLose") >= 0;
            testTimeout = Array.IndexOf(args, "-raidTestTimeout") >= 0;
            testRevive = Array.IndexOf(args, "-raidTestRevive") >= 0;
            testChain = Array.IndexOf(args, "-raidTestChain") >= 0;
            testCooperation = Array.IndexOf(args, "-raidTestCooperation") >= 0;
            testBoss = Array.IndexOf(args, "-raidTestBoss") >= 0;
            testPing = Array.IndexOf(args, "-raidTestPing") >= 0;
            autoReturn = Array.IndexOf(args, "-raidAutoReturn") >= 0;
            if (dedicatedServer)
            {
                transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
                Debug.Log("RAID_TEST: dedicated server started=" + manager.StartServer());
            }
            else if (Array.IndexOf(args, "-raidClient") >= 0)
            {
                transport.SetConnectionData("127.0.0.1", 7777);
                Debug.Log("RAID_TEST: client started=" + manager.StartClient());
            }
        }

        void Update()
        {
            if (manager.IsServer && testTutorial && !tutorialTestStarted && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Tutorial && RaidSession.Instance.Players.Count == 2)
            { tutorialTestStarted = true; StartCoroutine(RunTutorialSmoke()); }
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
            if (manager.IsServer && testCooperation && !cooperationTestStarted && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Combat && RaidSession.Instance.Players.Count == 2)
            { cooperationTestStarted = true; StartCoroutine(RunCooperationSmoke()); }
            if (manager.IsServer && testBoss && !bossTestStarted && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Combat && RaidSession.Instance.Players.Count == 2)
            { bossTestStarted = true; StartCoroutine(RunBossSmoke()); }
            if (testPing && pingTestIndex < 4 && manager.IsConnectedClient && RaidSession.Instance != null &&
                RaidSession.Instance.CurrentState == RaidSessionState.Combat)
            {
                if (nextPingTestTime <= 0f)
                    nextPingTestTime = Time.time + (manager.IsHost ? 0f : .45f);
                if (Time.time >= nextPingTestTime)
                {
                    RaidSession.Instance.SendPingRpc((RaidPing)pingTestIndex);
                    pingTestIndex++;
                    nextPingTestTime = Time.time + .9f;
                }
            }
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
            if ((!autoFlow && !autoReady) || !manager.IsConnectedClient || RaidSession.Instance == null ||
                !RaidSession.Instance.IsSpawned) return;
            var state = RaidSession.Instance.CurrentState;
            if (state == RaidSessionState.Lobby && !readySent && manager.LocalClient != null &&
                manager.LocalClient.PlayerObject != null)
            { readySent = true; RaidSession.Instance.SetReadyRpc(true); }
            if (autoFlow && state == RaidSessionState.Tutorial && !tutorialSent)
            { tutorialSent = true; RaidSession.Instance.CompleteTutorialRpc(); }
            if (state == RaidSessionState.Combat && tutorialSent)
            { Debug.Log("RAID_TEST: combat entered"); autoFlow = false; }
        }

        IEnumerator RunTutorialSmoke()
        {
            var session = RaidSession.Instance;
            var first = session.Players[0];
            var second = session.Players[1];
            var boss = RaidBoss.Instance;
            TeleportForTest(first, new Vector3(-4f, 1f, -4f));
            TeleportForTest(second, new Vector3(4f, 1f, -4f));
            yield return new WaitForSeconds(.3f);
            TeleportForTest(first, new Vector3(-1f, 1f, -4f));
            TeleportForTest(second, new Vector3(1f, 1f, -4f));
            yield return new WaitForSeconds(.3f);
            first.SetTestCooperation(ChainStance.Braced, false);
            second.SetTestCooperation(ChainStance.Braced, false);
            yield return new WaitForSeconds(.3f);
            first.SetTestCooperation(ChainStance.Flexible, false);
            second.SetTestCooperation(ChainStance.Flexible, false);
            yield return new WaitForSeconds(.3f);
            Vector3 leg = boss.transform.TransformPoint(RaidBoss.FrontLegAnchorLocal);
            TeleportForTest(first, new Vector3(leg.x - 3.2f, 1f, leg.z - 2.5f));
            TeleportForTest(second, new Vector3(leg.x + 3.2f, 1f, leg.z + 2.5f));
            yield return new WaitForSeconds(.3f);
            Debug.Log("TUTORIAL_SMOKE: wrap=" + RaidChain.Instance.ActiveAnchor.Value +
                " path=" + RaidChain.Instance.PathLength.Value.ToString("F1") +
                " leg=" + leg + " first=" + first.transform.position + " second=" + second.transform.position +
                " durability=" + boss.FrontLegDurability.Value);
            first.SetTestCooperation(ChainStance.Braced, true);
            second.SetTestCooperation(ChainStance.Braced, true);
            yield return new WaitForSeconds(3f);
            first.SetTestCooperation(ChainStance.Flexible, false);
            second.SetTestCooperation(ChainStance.Flexible, false);
            TeleportForTest(first, boss.transform.position + new Vector3(-4f, -1f, 0f));
            TeleportForTest(second, boss.transform.position + new Vector3(4f, -1f, 0f));
            yield return new WaitUntil(() => session.TutorialStep.Value >= 4 ||
                boss.CurrentAttack.Value == BossAttack.ChainSnatch && boss.Stage.Value == BossAttackStage.Telegraph);
            if (session.TutorialStep.Value == 3)
            {
                TeleportForTest(first, boss.transform.position + new Vector3(-1f, -1f, -3f));
                TeleportForTest(second, boss.transform.position + new Vector3(1f, -1f, -3f));
            }
            yield return new WaitUntil(() => session.TutorialStep.Value >= 4);
            TeleportForTest(second, boss.transform.position + new Vector3(0f, -1f, -2.5f));
            yield return new WaitUntil(() => second.Action.Value == RaidAction.Grabbed);
            Vector3 rescue = boss.RescuePoint;
            TeleportForTest(first, new Vector3(rescue.x, 1f, rescue.z));
            first.SetTestReviveHold(true);
            yield return new WaitUntil(() => session.CurrentState == RaidSessionState.Combat);
            first.SetTestReviveHold(false);
            Debug.Log("TUTORIAL_SMOKE: completed all five steps");
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
            Vector3 leg = RaidBoss.Instance.transform.TransformPoint(RaidBoss.FrontLegAnchorLocal);
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

        IEnumerator RunCooperationSmoke()
        {
            var first = RaidSession.Instance.Players[0];
            var second = RaidSession.Instance.Players[1];
            var chain = RaidChain.Instance;
            var cooperation = RaidCooperation.Instance;
            if (chain == null || cooperation == null)
            { Debug.LogError("COOP_SMOKE: missing component"); yield break; }
            Vector3 leg = RaidBoss.Instance.transform.TransformPoint(RaidBoss.FrontLegAnchorLocal);
            TeleportForTest(first, new Vector3(leg.x - 3.2f, 1f, leg.z - 2.5f));
            TeleportForTest(second, new Vector3(leg.x + 3.2f, 1f, leg.z + 2.5f));
            yield return new WaitForSeconds(.3f);
            bool wrapped = chain.ActiveAnchor.Value == RaidChainAnchor.FrontLeg;
            first.SetTestCooperation(ChainStance.Braced, true);
            second.SetTestCooperation(ChainStance.Braced, false);
            yield return new WaitForSeconds(.2f);
            bool soloBlocked = cooperation.Gauge.Value == 0f &&
                (cooperation.FailureReasons.Value & PullFailure.NoSharedHold) != 0;
            second.SetTestCooperation(ChainStance.Braced, true);
            yield return new WaitForSeconds(2.5f);
            bool staggered = RaidBoss.Instance.Staggered.Value;
            bool success = RaidSession.Instance.ChainSuccesses.Value == 1 &&
                RaidBoss.Instance.StaggerCount.Value == 1;
            first.SetTestCooperation(ChainStance.Braced, true);
            second.SetTestCooperation(ChainStance.Braced, true);
            yield return new WaitForSeconds(.2f);
            bool immune = (cooperation.FailureReasons.Value & PullFailure.PartImmune) != 0;
            Debug.Log("COOP_SMOKE: wrapped=" + wrapped + " soloBlocked=" + soloBlocked +
                " success=" + success + " staggered=" + staggered + " immune=" + immune);
            if (!wrapped || !soloBlocked || !success || !staggered || !immune)
                Debug.LogError("COOP_SMOKE: failed");
        }

        IEnumerator RunBossSmoke()
        {
            var boss = RaidBoss.Instance;
            var first = RaidSession.Instance.Players[0];
            var second = RaidSession.Instance.Players[1];
            TeleportForTest(first, boss.transform.position + new Vector3(0f, -1f, -3f));
            TeleportForTest(second, boss.transform.position + new Vector3(6f, -1f, 0f));
            boss.ForceAttackForTest(BossAttack.Stomp);
            bool facing = Vector3.Dot(boss.transform.forward, boss.AttackDirection.Value) > .99f;
            yield return new WaitForSeconds(boss.settings.stompTelegraph + .2f);
            bool stomp = first.Health.Value < first.maxHealth;
            bool expandedStomp = second.Health.Value < second.maxHealth;
            float beforeSweep = first.Health.Value;
            boss.ForceAttackForTest(BossAttack.TrunkSweep);
            yield return new WaitForSeconds(boss.settings.sweepTelegraph + .2f);
            bool sweep = first.Health.Value < beforeSweep;
            TeleportForTest(first, boss.transform.position + new Vector3(0f, -1f, -3f));
            Vector3 beforeCharge = boss.transform.position;
            boss.ForceAttackForTest(BossAttack.Charge);
            yield return new WaitForSeconds(boss.settings.chargeTelegraph + 1.1f);
            bool charge = Vector3.Distance(beforeCharge, boss.transform.position) > 3f;
            TeleportForTest(first, boss.transform.position + new Vector3(-2f, -1f, 0f));
            TeleportForTest(second, boss.transform.position + new Vector3(2f, -1f, 0f));
            boss.ForceAttackForTest(BossAttack.ChainSnatch);
            yield return new WaitForSeconds(boss.settings.snatchTelegraph + .2f);
            bool snatchAvoided = boss.Stage.Value == BossAttackStage.Recovery;
            first.Health.Value = second.Health.Value = 100f;
            first.Action.Value = second.Action.Value = RaidAction.Free;
            TeleportForTest(first, boss.transform.position + new Vector3(-4f, -1f, 0f));
            TeleportForTest(second, boss.transform.position + new Vector3(4f, -1f, 0f));
            boss.ForceAttackForTest(BossAttack.ChainSnatch);
            yield return new WaitForSeconds(boss.settings.snatchTelegraph + .2f);
            bool snatchActive = boss.Stage.Value == BossAttackStage.Active &&
                RaidChain.Instance.ActiveAnchor.Value == RaidChainAnchor.Trunk;
            yield return new WaitForSeconds(boss.settings.snatchResponse + 1f);
            bool snatchHit = first.Health.Value < 100f && second.Health.Value < 100f;
            first.Health.Value = second.Health.Value = 100f;
            first.Action.Value = second.Action.Value = RaidAction.Free;
            // Keep the long chain taut while both players remain within its hard limit
            // after the trunk anchor follows the boss's facing direction.
            Vector3 nearTrunk = boss.transform.position + boss.transform.forward * 4f;
            Vector3 oppositeSide = boss.transform.position + boss.transform.right * 6f;
            TeleportForTest(first, new Vector3(nearTrunk.x, 1f, nearTrunk.z));
            TeleportForTest(second, new Vector3(oppositeSide.x, 1f, oppositeSide.z));
            boss.ForceAttackForTest(BossAttack.ChainSnatch);
            yield return new WaitForSeconds(boss.settings.snatchTelegraph + .2f);
            bool snatchResponseOpen = boss.Stage.Value == BossAttackStage.Active;
            first.SetTestCooperation(ChainStance.Braced, true);
            second.SetTestCooperation(ChainStance.Braced, true);
            yield return new WaitForSeconds(2.2f);
            bool snatchCountered = snatchResponseOpen && boss.Staggered.Value &&
                first.Health.Value == 100f && second.Health.Value == 100f &&
                RaidChain.Instance.ActiveAnchor.Value != RaidChainAnchor.Trunk;
            first.SetTestCooperation(ChainStance.Flexible, false);
            second.SetTestCooperation(ChainStance.Flexible, false);
            TeleportForTest(first, boss.transform.position + new Vector3(3.5f, -1f, 0f));
            TeleportForTest(second, boss.transform.position + new Vector3(0f, -1f, -2.5f));
            boss.ForceAttackForTest(BossAttack.SpinGrab);
            Vector3 rescue = boss.RescuePoint;
            TeleportForTest(first, new Vector3(rescue.x, 1f, rescue.z));
            yield return new WaitForSeconds(boss.settings.spinTelegraph + .2f);
            bool spinGrabbed = second.Action.Value == RaidAction.Grabbed && first.Action.Value != RaidAction.Grabbed;
            first.SetTestReviveHold(true);
            yield return new WaitForSeconds(boss.settings.spinRescueSeconds + .3f);
            first.SetTestReviveHold(false);
            bool spinRescued = second.Action.Value == RaidAction.Free && second.Health.Value == 100f;
            first.Health.Value = second.Health.Value = 100f;
            boss.ForceAttackForTest(BossAttack.SpinGrab);
            yield return new WaitForSeconds(boss.settings.spinTelegraph + boss.settings.spinDuration + .3f);
            bool spinSafe = first.Health.Value == 100f;
            bool spinLanding = second.Health.Value < 100f && second.Action.Value != RaidAction.Grabbed;
            boss.Health.Value = boss.maxHealth * .49f;
            yield return null;
            bool phaseTwo = boss.Phase.Value == 2;
            Debug.Log("BOSS_SMOKE: facing=" + facing + " expandedStomp=" + expandedStomp +
                " stomp=" + stomp + " sweep=" + sweep + " charge=" + charge +
                " snatchAvoided=" + snatchAvoided + " snatchActive=" + snatchActive +
                " snatchHit=" + snatchHit + " snatchCountered=" + snatchCountered +
                " spinGrabbed=" + spinGrabbed +
                " spinRescued=" + spinRescued + " spinSafe=" + spinSafe +
                " spinLanding=" + spinLanding + " phaseTwo=" + phaseTwo);
            if (!facing || !expandedStomp || !stomp || !sweep || !charge || !snatchAvoided || !snatchActive ||
                !snatchHit || !snatchCountered || !spinGrabbed || !spinRescued ||
                !spinSafe || !spinLanding || !phaseTwo)
                Debug.LogError("BOSS_SMOKE: failed");
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
            if (!manager.IsClient && manager.IsListening) return;
            GUI.skin.label.fontSize = 15;
            GUI.skin.button.fontSize = 15;
            GUI.skin.button.fixedHeight = 30;
            GUI.skin.textField.fontSize = 15;
            GUI.skin.textField.fixedHeight = 28;
            GUILayout.BeginArea(new Rect(12, 12, 500, 515), GUI.skin.box);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label("CHAINBOUND RAID | Developer Session");
#else
            GUILayout.Label("CHAINBOUND RAID | Test Session");
#endif
            if (!manager.IsListening)
            {
                GUILayout.Label("Server address:");
                address = GUILayout.TextField(address);
                if (GUILayout.Button("Join server (port 7777)"))
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
                    bool ready = manager.LocalClientId == session.FirstPlayerClientId.Value ?
                        session.FirstReady.Value : session.SecondReady.Value;
                    session.SetReadyRpc(!ready);
                }
            }
            else if (session.CurrentState == RaidSessionState.Tutorial)
            {
                GUILayout.Label("TUTORIAL " + (session.TutorialStep.Value + 1) + "/5 — both players share progress");
                switch (session.TutorialStep.Value)
                {
                    case 0:
                        GUILayout.Label(session.TutorialProgress.Value == 0 ?
                            "Move apart until the chain turns yellow (7 m)." :
                            "Good. Move together until the chain relaxes.");
                        GUILayout.Label("Chain: " + (RaidChain.Instance != null ? RaidChain.Instance.DistanceBand.Value.ToString() : "waiting"));
                        break;
                    case 1:
                        GUILayout.Label("Each player: press Q to brace, then Q again to relax.");
                        int progress = session.TutorialProgress.Value;
                        GUILayout.Label("Player 1: " + ((progress & 4) != 0 ? "done" : (progress & 1) != 0 ? "relax with Q" : "brace with Q"));
                        GUILayout.Label("Player 2: " + ((progress & 8) != 0 ? "done" : (progress & 2) != 0 ? "relax with Q" : "brace with Q"));
                        break;
                    case 2:
                        GUILayout.Label("Follow the blue markers to opposite sides of the front leg.");
                        GUILayout.Label("Both press Q to brace, then hold F together.");
                        if (RaidCooperation.Instance != null)
                        {
                            var cooperation = RaidCooperation.Instance;
                            GUILayout.Label("Pull: " + cooperation.Gauge.Value.ToString("F0") + " / 100");
                            GUILayout.Label("Hint: " + TutorialPullHint(cooperation.FailureReasons.Value));
                        }
                        break;
                    case 3:
                        GUILayout.Label("Keep the chain taut to trigger the practice snatch.");
                        GUILayout.Label("Chain snatch: approach before it catches the chain,");
                        GUILayout.Label("or brace and hold F together to counter it.");
                        if (RaidChain.Instance != null)
                            GUILayout.Label("Chain: " + RaidChain.Instance.PathLength.Value.ToString("F1") + " / 7.0 m");
                        break;
                    case 4:
                        bool rescuer = manager.LocalClientId == session.FirstPlayerClientId.Value;
                        GUILayout.Label(rescuer ?
                            "Your role: follow the green marker and hold E to rescue." :
                            "Your role: approach the boss; when grabbed, hold R to resist.");
                        if (RaidBoss.Instance != null)
                            GUILayout.Label("Rescue: " + RaidBoss.Instance.SpinRescueProgress.Value.ToString("F1") + " s");
                        break;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (GUILayout.Button("Developer: skip tutorial")) session.CompleteTutorialRpc();
#endif
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
                    GUILayout.Label("Stance " + local.Stance.Value +
                        (local.StanceChanging.Value ? " (switching)" : "") +
                        " | Pull " + (local.PullHeld.Value ? "held" : "released"));
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
                if (RaidCooperation.Instance != null && RaidCooperation.Instance.IsSpawned)
                    GUILayout.Label("Suppression " + RaidCooperation.Instance.Gauge.Value.ToString("F0") +
                        " / 100 | " + RaidCooperationRules.Primary(
                            RaidCooperation.Instance.FailureReasons.Value));
                GUILayout.Label("WASD move | Mouse look | Shift sprint | Space dodge");
                GUILayout.Label("Q stance | F pull | E revive | Tab target part");
                GUILayout.Label("Left/Right click attacks");
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
            if (session.CurrentState == RaidSessionState.Tutorial) DrawTutorialMarkers(session);
        }

        static string TutorialPullHint(PullFailure failures)
        {
            return RaidCooperationRules.Primary(failures) switch
            {
                PullFailure.NoAnchor => "Put the front leg between the chain and both players.",
                PullFailure.NotBraced => "Both players press Q to brace.",
                PullFailure.NotGrounded => "Stay on the ground.",
                PullFailure.InvalidAngle => "Move to opposite sides of the boss.",
                PullFailure.NoSharedHold => "Both players hold F at the same time.",
                PullFailure.PartImmune => "Wait for the leg to recover.",
                PullFailure.PlayerDisabled => "Wait until both players can act.",
                _ => "Wrap the front leg, then brace and pull together."
            };
        }

        void DrawTutorialMarkers(RaidSession session)
        {
            var boss = RaidBoss.Instance;
            if (boss == null) return;
            if (session.TutorialStep.Value == 2)
            {
                Vector3 leg = boss.transform.TransformPoint(RaidBoss.FrontLegAnchorLocal);
                bool first = manager.LocalClientId == session.FirstPlayerClientId.Value;
                Vector3 offset = first ? new Vector3(-3.2f, 0f, -2.5f) : new Vector3(3.2f, 0f, 2.5f);
                DrawWorldMarker(leg + offset, "YOUR POSITION", new Color(.3f, .8f, 1f));
                DrawWorldMarker(leg, "FRONT LEG", new Color(1f, .8f, .25f));
            }
            else if (session.TutorialStep.Value == 4 &&
                     manager.LocalClientId == session.FirstPlayerClientId.Value)
                DrawWorldMarker(boss.RescuePoint, "RESCUE POINT", new Color(.35f, 1f, .45f));
        }

        static void DrawWorldMarker(Vector3 worldPosition, string label, Color color)
        {
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 screen = camera.WorldToScreenPoint(worldPosition + Vector3.up * 1.5f);
            if (screen.z <= 0f) return;
            float x = Mathf.Clamp(screen.x, 80f, Screen.width - 80f);
            float y = Mathf.Clamp(Screen.height - screen.y, 40f, Screen.height - 40f);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(new Rect(x - 80f, y - 18f, 160f, 36f), label);
            GUI.color = previous;
        }
    }
}
