using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public sealed class RaidHud : MonoBehaviour
    {
        float subtitleScale = 1f;
        bool screenShake = true;
        AudioSource audioSource;
        AudioClip cue;
        int lastPingSequence;
        int lastAttackSequence;
        GUIStyle labelStyle;
        GUIStyle hintStyle;

        public static bool ScreenShakeEnabled => PlayerPrefs.GetInt("RaidScreenShake", 1) == 1;

        void Awake()
        {
            subtitleScale = PlayerPrefs.GetFloat("RaidSubtitleScale", 1f);
            screenShake = ScreenShakeEnabled;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = .22f;
            cue = AudioClip.Create("Raid cue", 2205, 1, 22050, false);
            float[] samples = new float[2205];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(i * 2f * Mathf.PI * 660f / 22050f) *
                    Mathf.Sin(Mathf.PI * i / samples.Length);
            cue.SetData(samples, 0);
        }

        void Update()
        {
            var session = RaidSession.Instance;
            if (session == null || !session.IsSpawned || session.CurrentState != RaidSessionState.Combat) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) session.SendPingRpc(RaidPing.Approach);
            if (Input.GetKeyDown(KeyCode.Alpha2)) session.SendPingRpc(RaidPing.Pull);
            if (Input.GetKeyDown(KeyCode.Alpha3)) session.SendPingRpc(RaidPing.Release);
            if (Input.GetKeyDown(KeyCode.Alpha4)) session.SendPingRpc(RaidPing.Rescue);
            if (session.PingSequence.Value != lastPingSequence)
            {
                lastPingSequence = session.PingSequence.Value;
                audioSource.PlayOneShot(cue);
                Debug.Log("HUD_EVENT: PingReceived kind=" + session.PingKind.Value +
                    " sender=" + session.PingSender.Value + " tick=" + session.PingServerTick.Value);
            }
            var boss = RaidBoss.Instance;
            if (boss != null && boss.AttackSequence.Value != lastAttackSequence)
            { lastAttackSequence = boss.AttackSequence.Value; audioSource.PlayOneShot(cue); }
        }

        void OnGUI()
        {
            var session = RaidSession.Instance;
            if (session == null || !session.IsSpawned || session.CurrentState != RaidSessionState.Combat) return;
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.LocalClient == null || manager.LocalClient.PlayerObject == null) return;
            var local = manager.LocalClient.PlayerObject.GetComponent<RaidPlayer>();
            if (local == null) return;
            RaidPlayer partner = null;
            foreach (var player in FindObjectsByType<RaidPlayer>(FindObjectsSortMode.None))
                if (player != local && player.IsSpawned) { partner = player; break; }
            var boss = RaidBoss.Instance;
            float x = Mathf.Max(8f, Screen.width - 388f), y = 12f;
            GUI.Box(new Rect(x, y, 376f, 426f), "RAID STATUS");
            EnsureStyles();
            y += 25f;
            Label(x + 12f, ref y, "YOU  " + local.Action.Value + "  " + local.Stance.Value, Color.white);
            Bar(x + 12f, ref y, "HP", local.Health.Value, local.maxHealth, new Color(.9f, .22f, .22f));
            Bar(x + 12f, ref y, "ST", local.Stamina.Value, local.maxStamina, new Color(.3f, .8f, .4f));
            if (partner != null)
            {
                Label(x + 12f, ref y, "PARTNER  " + partner.Action.Value + "  " + PartnerDirection(partner), Color.white);
                Bar(x + 12f, ref y, "HP", partner.Health.Value, partner.maxHealth, new Color(1f, .5f, .18f));
                Bar(x + 12f, ref y, "ST", partner.Stamina.Value, partner.maxStamina, new Color(.3f, .8f, .4f));
            }
            if (boss != null)
            {
                Label(x + 12f, ref y, "BOSS PHASE " + boss.Phase.Value +
                    (boss.Staggered.Value ? "  STAGGERED" : ""), new Color(1f, .8f, .35f));
                Bar(x + 12f, ref y, "HP", boss.Health.Value, boss.maxHealth, new Color(.7f, .18f, .8f));
                Bar(x + 12f, ref y, "TRUNK", boss.TrunkDurability.Value, boss.partMaxDurability, Color.yellow);
                Bar(x + 12f, ref y, "LEG", boss.FrontLegDurability.Value, boss.partMaxDurability, Color.yellow);
            }
            var chain = RaidChain.Instance;
            if (chain != null)
                Label(x + 12f, ref y, "CHAIN " + chain.PathLength.Value.ToString("F1") + "m  " +
                    chain.DisplayState.Value + "  " + chain.ActiveAnchor.Value, Color.cyan);
            var cooperation = RaidCooperation.Instance;
            if (cooperation != null)
            {
                float max = cooperation.settings != null ? cooperation.settings.gaugeMax : 100f;
                Bar(x + 12f, ref y, "PULL", cooperation.Gauge.Value, max, new Color(.35f, .9f, 1f));
                if (cooperation.FailureReasons.Value != PullFailure.None)
                    Label(x + 12f, ref y, "PULL: " + RaidCooperationRules.Primary(cooperation.FailureReasons.Value),
                        new Color(1f, .75f, .25f));
            }
            if (boss != null && boss.Stage.Value == BossAttackStage.Telegraph)
            {
                double left = boss.TelegraphEndServerTime.Value - manager.ServerTime.Time;
                Label(x + 12f, ref y, "! " + boss.CurrentAttack.Value + "  " +
                    Mathf.Max(0f, (float)left).ToString("F1") + "s", Color.red, true);
            }
            else if (boss != null && boss.Stage.Value == BossAttackStage.Active)
                Label(x + 12f, ref y, ActiveHint(boss, local), Color.yellow, true);
            if (session.PingSequence.Value > 0 && manager.ServerTime.Time - session.PingServerTime.Value <
                session.pingDisplaySeconds)
                Label(x + 12f, ref y, "PING " + session.PingKind.Value + " from P" +
                    session.PingSender.Value + " @" + session.PingServerTick.Value, Color.green, true);
            if (local.IsDown)
                Label(x + 12f, ref y, "REVIVE " + local.ReviveProgress.Value.ToString("F1") +
                    "/" + local.RequiredReviveSeconds.ToString("F0") + "s", Color.red, true);

            float bottom = Screen.height - 52f;
            GUI.Box(new Rect(12f, bottom - 25f, 565f, 76f), "PING  1 Approach   2 Pull   3 Release   4 Rescue");
            if (GUI.Button(new Rect(20f, bottom, 42f, 27f), "A-")) SetSubtitleScale(subtitleScale - .1f);
            if (GUI.Button(new Rect(66f, bottom, 42f, 27f), "A+")) SetSubtitleScale(subtitleScale + .1f);
            if (GUI.Button(new Rect(116f, bottom, 160f, 27f),
                screenShake ? "Screen shake: ON" : "Screen shake: OFF"))
            {
                screenShake = !screenShake;
                PlayerPrefs.SetInt("RaidScreenShake", screenShake ? 1 : 0);
            }
        }

        string ActiveHint(RaidBoss boss, RaidPlayer local)
        {
            if (boss.CurrentAttack.Value == BossAttack.ChainSnatch)
                return boss.SnatchPulling.Value ? "! Chain pull - move toward boss" : "! Brace and pull together";
            if (boss.CurrentAttack.Value == BossAttack.SpinGrab)
                return local.Action.Value == RaidAction.Grabbed ? "! Hold R to slow spin" :
                    "! Reach rescue marker and hold E  " + boss.SpinRescueProgress.Value.ToString("F1") + "s";
            return "! " + boss.CurrentAttack.Value;
        }

        string PartnerDirection(RaidPlayer partner)
        {
            Camera camera = Camera.main;
            if (camera == null) return "";
            Vector3 viewport = camera.WorldToViewportPoint(partner.transform.position + Vector3.up);
            float distance = Vector3.Distance(camera.transform.position, partner.transform.position);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f) return distance.ToString("F0") + "m";
            Vector3 relative = camera.transform.InverseTransformPoint(partner.transform.position);
            string arrow = Mathf.Abs(relative.x) > Mathf.Abs(relative.y) ?
                (relative.x > 0f ? ">" : "<") : (relative.y > 0f ? "^" : "v");
            if (relative.z < 0f) arrow = relative.x > 0f ? ">" : "<";
            return arrow + " " + distance.ToString("F0") + "m";
        }

        void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            }
            hintStyle.fontSize = Mathf.RoundToInt(18f * subtitleScale);
        }

        void Label(float x, ref float y, string value, Color color, bool hint = false)
        {
            var style = hint ? hintStyle : labelStyle;
            style.normal.textColor = color;
            GUI.Label(new Rect(x, y, 355f, hint ? 29f : 21f), value, style);
            y += hint ? 27f : 20f;
        }

        void Bar(float x, ref float y, string label, float value, float maximum, Color color)
        {
            GUI.Label(new Rect(x, y, 52f, 20f), label, labelStyle);
            GUI.color = new Color(.1f, .1f, .1f, .85f);
            GUI.DrawTexture(new Rect(x + 56f, y + 4f, 235f, 13f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x + 56f, y + 4f,
                235f * Mathf.Clamp01(value / Mathf.Max(1f, maximum)), 13f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 296f, y, 70f, 20f), value.ToString("F0"));
            y += 19f;
        }

        void SetSubtitleScale(float value)
        {
            subtitleScale = Mathf.Clamp(value, .8f, 1.6f);
            PlayerPrefs.SetFloat("RaidSubtitleScale", subtitleScale);
        }
    }
}
