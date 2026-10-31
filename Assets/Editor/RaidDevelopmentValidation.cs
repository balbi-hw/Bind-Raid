using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class RaidDevelopmentValidation
    {
        [Serializable] class Message { public string id, english, korean; }
        [Serializable] class Messages { public Message[] entries; }
        [MenuItem("Chainbound Raid/Validate Gameplay Improvements")]
        public static void Validate()
        {
            RaidMovementValidation.Validate(); RaidCombatValidation.Validate();
            ValidateLanguage(); ValidateDecision(); ValidatePresentation(); ValidateCamera(); ValidateAssets();
            PrototypeChainValidation.Validate(); PrototypeCooperationValidation.Validate();
            Debug.Log("IMPROVEMENT_VALIDATION: presentation, camera, settings, language, chain, cooperation and decision checks passed");
        }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void ValidateLanguage()
        {
            var resource = Resources.Load<TextAsset>("RaidLocalization"); Check(resource != null, "Localization catalog missing.");
            var ids = new HashSet<string>();
            foreach (var item in JsonUtility.FromJson<Messages>(resource.text).entries)
            {
                Check(ids.Add(item.id), "Duplicate localization id: " + item.id);
                var first = Regex.Matches(item.english, @"\{\d+\}"); var second = Regex.Matches(item.korean, @"\{\d+\}");
                Check(first.Count == second.Count, "Translation arguments differ: " + item.id);
                for (int i = 0; i < first.Count; i++) Check(first[i].Value == second[i].Value, "Translation argument lost: " + item.id);
            }
            bool had = PlayerPrefs.HasKey("RaidLanguage"); int previous = PlayerPrefs.GetInt("RaidLanguage", 0);
            try
            {
                PlayerPrefs.SetInt("RaidLanguage", 1); Check(RaidLanguage.Get("settings.title") == "설정", "Korean selection failed.");
                PlayerPrefs.SetInt("RaidLanguage", 0); Check(RaidLanguage.Get("settings.title") == "Settings", "English selection failed.");
                Check(RaidLanguage.Value("Attack") == "Attack", "English enum rendering failed.");
            }
            finally { if (had) PlayerPrefs.SetInt("RaidLanguage", previous); else PlayerPrefs.DeleteKey("RaidLanguage"); }
        }
        static void ValidateDecision()
        {
            var set = AssetDatabase.LoadAssetAtPath<RaidBossMoveSet>("Assets/Settings/MammothMoveSet.asset");
            Check(set != null && set.moves.Length > 0, "Boss move data missing.");
            var ready = new Dictionary<BossAttack, float>();
            var context = new RaidBossDecisionContext { targetAvailable = true, distance = 6.5f, phase = 1, now = 20f,
                chainAvailable = true, chainTaut = true };
            for (int i = 0; i <= 100; i++)
            {
                var selected = RaidBossDecision.Select(set, context, ready, i / 100f);
                Check(selected != BossAttack.None, "No eligible move selected.");
                Check(RaidBossDecision.Weight(set.Get(selected), context, 0f) > 0f, "Ineligible move selected.");
            }
            var snatch = set.Get(BossAttack.ChainSnatch);
            context.chainTaut = false; Check(RaidBossDecision.Weight(snatch, context, 0f) == 0f, "Snatch selected without taut chain.");
            context.chainTaut = true; context.previous = BossAttack.ChainSnatch;
            Check(RaidBossDecision.Weight(snatch, context, 0f) == 0f, "Same control repeated.");
            context.previous = BossAttack.None; context.controlActive = true;
            Check(RaidBossDecision.Weight(snatch, context, 0f) == 0f, "Control overlapped.");
            context.controlActive = false; context.controlReadyAt = 21f;
            Check(RaidBossDecision.Weight(snatch, context, 0f) == 0f, "Control rest ignored.");
            context.controlReadyAt = 0f; Check(RaidBossDecision.Weight(snatch, context, 21f) == 0f, "Cooldown ignored.");
            context.targetAvailable = false; Check(RaidBossDecision.Select(set, context, ready, .5f) == BossAttack.None, "Targetless attack selected.");
            var arena = ScriptableObject.CreateInstance<RaidArenaSettings>();
            arena.center = new Vector3(100f, 0f, 100f); arena.halfExtents = new Vector2(3f, 5f);
            Check(arena.Clamp(new Vector3(110f, 2f, 90f)) == new Vector3(103f, 2f, 95f), "Arena depends on origin coordinates.");
            UnityEngine.Object.DestroyImmediate(arena);
        }
        static void ValidatePresentation()
        {
            var root = new GameObject("AnimationValidation"); root.transform.position = Vector3.one * 100f;
            var joint = new GameObject("Joint"); joint.transform.SetParent(root.transform, false);
            var animator = root.AddComponent<Animator>();
            var first = new AnimationClip(); first.SetCurve("Joint", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, 1f, 0f));
            var second = new AnimationClip(); second.SetCurve("Joint", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, 1f, 1f));
            var profile = ScriptableObject.CreateInstance<RaidCharacterPresentation>();
            profile.idle = profile.walk = profile.sprint = first;
            RaidAnimationDriver driver = null;
            try
            {
                driver = new RaidAnimationDriver(animator, profile);
                driver.Evaluate(0f, first, 0, 0f, false, .2f);
                driver.Evaluate(0f, second, 1, .5f, true, .06f);
                Check(joint.transform.localPosition.y > 0f && joint.transform.localPosition.y < 1f, "Animation change did not blend poses.");
                driver.Evaluate(0f, second, 1, .8f, true, .2f);
                Check(Mathf.Abs(joint.transform.localPosition.y - 1f) < .01f, "Animation target pose did not settle.");
                driver.Evaluate(5f, null, 0, 0f, false, .2f);
                Check(Mathf.Abs(joint.transform.localPosition.y) < .01f, "Locomotion did not resume.");
                driver.Dispose(); driver.Dispose();
            }
            finally
            {
                driver?.Dispose(); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(profile);
            }
        }
        static void ValidateCamera()
        {
            var target = new GameObject("CameraTarget"); target.transform.position = new Vector3(100f, 1f, 100f);
            var self = new GameObject("SelfCollider"); self.transform.SetParent(target.transform, false);
            self.transform.localPosition = new Vector3(0f, 1.7f, -1f); self.AddComponent<SphereCollider>();
            var wall = new GameObject("CameraWall"); wall.transform.position = new Vector3(100f, 3f, 98f);
            wall.AddComponent<BoxCollider>().size = new Vector3(6f, 5f, .1f);
            var cameraObject = new GameObject("CameraValidation"); cameraObject.AddComponent<Camera>();
            var settings = ScriptableObject.CreateInstance<RaidCameraSettings>();
            var camera = cameraObject.AddComponent<PrototypeCamera>(); camera.target = target.transform; camera.settings = settings;
            try
            {
                typeof(PrototypeCamera).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, null);
                Physics.SyncTransforms();
                typeof(PrototypeCamera).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, null);
                float distance = Vector3.Distance(cameraObject.transform.position, target.transform.position + Vector3.up * settings.focusHeight);
                Check(distance > 1.2f && distance < 2.5f, "Camera ignored wall or collided with its own actor.");
            }
            finally
            { UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(settings); }
        }
        static void ValidateAssets()
        {
            foreach (var variant in new[] { "A", "B" })
            {
                var profile = AssetDatabase.LoadAssetAtPath<RaidCharacterPresentation>("Assets/Settings/Player" + variant + "Presentation.asset");
                Check(profile != null && profile.idle != null && profile.walk != null && profile.sprint != null &&
                    profile.weakAttack != null && profile.strongAttack != null, "Character presentation references missing: " + variant);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkRaidPlayer.prefab");
            Check(prefab != null && prefab.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null, "Duplicate player transform writer.");
            Check(prefab.GetComponent<RaidPlayerVisual>().firstPresentation != null, "Presentation profile not assigned.");
            var boss = AssetDatabase.LoadAssetAtPath<RaidBossSettings>("Assets/Settings/BossSettings.asset");
            Check(boss != null && boss.anchors != null && boss.moveSet != null && boss.arena != null, "Boss profile references missing.");
        }
    }
}
