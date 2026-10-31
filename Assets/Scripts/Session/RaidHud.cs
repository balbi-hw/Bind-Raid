using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public sealed class RaidHud : MonoBehaviour
    {
        float subtitleScale = 1f;
        AudioSource audioSource;
        AudioClip cue;
        int lastPingSequence;
        int lastAttackSequence;
        GUIStyle labelStyle;
        GUIStyle hintStyle;

        public static bool ScreenShakeEnabled => RaidUserSettings.ScreenShake;

        void Awake()
        {
            subtitleScale = PlayerPrefs.GetFloat("RaidSubtitleScale", 1f);
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
            if (RaidInputContext.AcceptingInput && Input.GetKeyDown(KeyCode.Alpha1)) session.SendPingRpc(RaidPing.Approach);
            if (RaidInputContext.AcceptingInput && Input.GetKeyDown(KeyCode.Alpha2)) session.SendPingRpc(RaidPing.Pull);
            if (RaidInputContext.AcceptingInput && Input.GetKeyDown(KeyCode.Alpha3)) session.SendPingRpc(RaidPing.Release);
            if (RaidInputContext.AcceptingInput && Input.GetKeyDown(KeyCode.Alpha4)) session.SendPingRpc(RaidPing.Rescue);
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
            if (RaidInputContext.SettingsOpen) return;
            subtitleScale = RaidUserSettings.SubtitleScale;
            var session = RaidSession.Instance;
            if (session == null || !session.IsSpawned || session.CurrentState != RaidSessionState.Combat) return;
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.LocalClient == null || manager.LocalClient.PlayerObject == null) return;
            var local = manager.LocalClient.PlayerObject.GetComponent<RaidPlayer>();
            if (local == null) return;
            RaidPlayer partner = null;
            foreach (var player in RaidPlayerRegistry.Active)
                if (player != local && player.IsSpawned) { partner = player; break; }
            var boss = RaidBoss.Instance;
            float x = Mathf.Max(8f, Screen.width - 388f), y = 12f;
            GUI.Box(new Rect(x, y, 376f, 426f),RaidLanguage.Get("ui.hud.020"));
            EnsureStyles();
            y += 25f;
            Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.019", RaidLanguage.Value(local.Action.Value), RaidLanguage.Value(local.Stance.Value)), Color.white);
            Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.014"), local.Health.Value, local.maxHealth, new Color(.9f, .22f, .22f));
            Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.017"), local.Stamina.Value, local.maxStamina, new Color(.3f, .8f, .4f));
            if (partner != null)
            {
                Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.018", RaidLanguage.Value(partner.Action.Value), RaidLanguage.Value(PartnerDirection(partner))), Color.white);
                Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.014"), partner.Health.Value, partner.maxHealth, new Color(1f, .5f, .18f));
                Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.017"), partner.Stamina.Value, partner.maxStamina, new Color(.3f, .8f, .4f));
            }
            if (boss != null)
            {
                Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.016", RaidLanguage.Value(boss.Phase.Value), (boss.Staggered.Value  ? RaidLanguage.Get("ui.hud.015") : "")), new Color(1f, .8f, .35f));
                Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.014"), boss.Health.Value, boss.maxHealth, new Color(.7f, .18f, .8f));
                Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.013"), boss.TrunkDurability.Value, boss.partMaxDurability, Color.yellow);
                Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.012"), boss.FrontLegDurability.Value, boss.partMaxDurability, Color.yellow);
            }
            var chain = RaidChain.Instance;
            if (chain != null)
                Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.011", RaidLanguage.Value(chain.PathLength.Value.ToString("F1")), RaidLanguage.Value(chain.DisplayState.Value), RaidLanguage.Value(chain.ActiveAnchor.Value)), Color.cyan);
            var cooperation = RaidCooperation.Instance;
            if (cooperation != null)
            {
                float max = cooperation.settings != null ? cooperation.settings.gaugeMax : 100f;
                Bar(x + 12f, ref y,RaidLanguage.Get("ui.hud.010"), cooperation.Gauge.Value, max, new Color(.35f, .9f, 1f));
                if (cooperation.FailureReasons.Value != PullFailure.None)
                    Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.009", RaidLanguage.Get(RaidCooperationRules.InstructionId(cooperation.FailureReasons.Value))),
                        new Color(1f, .75f, .25f));
            }
            if (boss != null && boss.Stage.Value == BossAttackStage.Telegraph)
            {
                double left = boss.TelegraphEndServerTime.Value - manager.ServerTime.Time;
                Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.008", RaidLanguage.Value(boss.CurrentAttack.Value), RaidLanguage.Value(Mathf.Max(0f, (float)left).ToString("F1"))), Color.red, true);
            }
            else if (boss != null && boss.Stage.Value == BossAttackStage.Active)
                Label(x + 12f, ref y,RaidLanguage.Value(ActiveHint(boss, local)), Color.yellow, true);
            if (session.PingSequence.Value > 0 && manager.ServerTime.Time - session.PingServerTime.Value <
                session.pingDisplaySeconds)
                Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.007", RaidLanguage.Value(session.PingKind.Value), RaidLanguage.Value(session.PingSender.Value), RaidLanguage.Value(session.PingServerTick.Value)), Color.green, true);
            if (local.IsDown)
                Label(x + 12f, ref y,RaidLanguage.Format("ui.hud.006", RaidLanguage.Value(local.ReviveProgress.Value.ToString("F1")), RaidLanguage.Value(local.RequiredReviveSeconds.ToString("F0"))), Color.red, true);

            GUI.Box(new Rect(12f, Screen.height - 46f, Mathf.Min(565f, Screen.width - 24f), 34f), RaidLanguage.Get("ui.hud.005"));
        }

        string ActiveHint(RaidBoss boss, RaidPlayer local)
        {
            if (boss.CurrentAttack.Value == BossAttack.ChainSnatch)
                return boss.SnatchPulling.Value ? RaidLanguage.Get("ui.hud.021") : RaidLanguage.Get("ui.hud.022");
            if (boss.CurrentAttack.Value == BossAttack.SpinGrab)
                return local.Action.Value == RaidAction.Grabbed ? RaidLanguage.Get("ui.hud.023") :
                    RaidLanguage.Get("ui.hud.024") + boss.SpinRescueProgress.Value.ToString("F1") + RaidLanguage.Get("ui.hud.025");
            return "! " + RaidLanguage.Value(boss.CurrentAttack.Value);
        }

        string PartnerDirection(RaidPlayer partner)
        {
            Camera camera = Camera.main;
            if (camera == null) return "";
            Vector3 viewport = camera.WorldToViewportPoint(partner.transform.position + Vector3.up);
            float distance = Vector3.Distance(camera.transform.position, partner.transform.position);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f) return distance.ToString("F0") + RaidLanguage.Get("ui.hud.026");
            Vector3 relative = camera.transform.InverseTransformPoint(partner.transform.position);
            string arrow = Mathf.Abs(relative.x) > Mathf.Abs(relative.y) ?
                (relative.x > 0f ? ">" : "<") : (relative.y > 0f ? "^" : RaidLanguage.Get("ui.hud.027"));
            if (relative.z < 0f) arrow = relative.x > 0f ? ">" : "<";
            return arrow + " " + distance.ToString("F0") + RaidLanguage.Get("ui.hud.026");
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
            GUI.Label(new Rect(x, y, 355f, hint ? 29f : 21f),RaidLanguage.Value(value), style);
            y += hint ? 27f : 20f;
        }

        void Bar(float x, ref float y, string label, float value, float maximum, Color color)
        {
            GUI.Label(new Rect(x, y, 52f, 20f),RaidLanguage.Value(label), labelStyle);
            GUI.color = new Color(.1f, .1f, .1f, .85f);
            GUI.DrawTexture(new Rect(x + 56f, y + 4f, 235f, 13f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x + 56f, y + 4f,
                235f * Mathf.Clamp01(value / Mathf.Max(1f, maximum)), 13f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 296f, y, 70f, 20f),RaidLanguage.Value(value.ToString("F0")));
            y += 19f;
        }

    }
}
