using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeCooperationSetup
    {
        [MenuItem("Chainbound Raid/Apply Cooperation")]
        public static void Apply()
        {
            const string assetPath = "Assets/Settings/CooperationSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<RaidCooperationSettings>(assetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RaidCooperationSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaidSessionScene.unity");
            GameObject gameObject = GameObject.Find("Raid Cooperation");
            if (gameObject == null) gameObject = new GameObject("Raid Cooperation");
            if (gameObject.GetComponent<NetworkObject>() == null) gameObject.AddComponent<NetworkObject>();
            var cooperation = gameObject.GetComponent<RaidCooperation>();
            if (cooperation == null) cooperation = gameObject.AddComponent<RaidCooperation>();
            cooperation.settings = settings;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Raid cooperation applied to raid scene.");
        }
    }
}
