using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeChainSetup
    {
        [MenuItem("Chainbound Raid/Apply Network Chain")]
        public static void Apply()
        {
            const string path = "Assets/Scenes/RaidSessionScene.unity";
            var scene = EditorSceneManager.OpenScene(path);
            GameObject chainObject = GameObject.Find("Raid Chain");
            if (chainObject == null) chainObject = new GameObject("Raid Chain");
            if (chainObject.GetComponent<NetworkObject>() == null) chainObject.AddComponent<NetworkObject>();
            LineRenderer line = chainObject.GetComponent<LineRenderer>();
            if (line == null) line = chainObject.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StatusUnlit.mat");
            line.useWorldSpace = true;
            line.widthMultiplier = .09f;
            line.positionCount = 2;
            line.enabled = false;
            if (chainObject.GetComponent<RaidChain>() == null) chainObject.AddComponent<RaidChain>();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Network raid chain applied to raid scene.");
        }
    }
}
