using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode.Components;

namespace ChainboundRaid.Editor
{
    public static class PrototypeBossSetup
    {
        [MenuItem("Chainbound Raid/Apply Boss Attacks")]
        public static void Apply()
        {
            const string settingsPath = "Assets/Settings/BossSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<RaidBossSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RaidBossSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaidSessionScene.unity");
            GameObject gameObject = GameObject.Find("Boss Training Dummy");
            if (gameObject == null) throw new System.InvalidOperationException("Boss is missing.");
            var boss = gameObject.GetComponent<RaidBoss>();
            boss.settings = settings;
            if (gameObject.GetComponent<NetworkTransform>() == null) gameObject.AddComponent<NetworkTransform>();
            Transform child = gameObject.transform.Find("Attack Telegraph");
            if (child == null)
            {
                child = new GameObject("Attack Telegraph").transform;
                child.SetParent(gameObject.transform, false);
            }
            var line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StatusUnlit.mat");
            line.useWorldSpace = true;
            line.widthMultiplier = .09f;
            line.enabled = false;
            gameObject.GetComponent<RaidBossVisual>().attackTelegraph = line;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Boss attack settings and telegraph applied.");
        }
    }
}
