using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeHudSetup
    {
        [MenuItem("Chainbound Raid/Apply Raid HUD")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaidSessionScene.unity");
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Main Camera is missing.");
            if (camera.GetComponent<RaidHud>() == null) camera.gameObject.AddComponent<RaidHud>();
            var boss = GameObject.Find("Boss Training Dummy");
            if (boss == null) throw new System.InvalidOperationException("Boss is missing.");
            Transform child = boss.transform.Find("Spin Rescue Ring");
            if (child == null)
            {
                child = new GameObject("Spin Rescue Ring").transform;
                child.SetParent(boss.transform, false);
            }
            var line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StatusUnlit.mat");
            line.useWorldSpace = true;
            line.widthMultiplier = .07f;
            line.enabled = false;
            boss.GetComponent<RaidBossVisual>().rescueRing = line;
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Raid HUD and rescue marker applied.");
        }
    }
}
