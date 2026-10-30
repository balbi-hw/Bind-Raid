using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeChainSetup
    {
        const string LinkPath = "Assets/ThirdParty/ChainKutejnikov/ChainLink.fbx";
        const string TexturePath = "Assets/ThirdParty/ChainKutejnikov/Chain_BaseColor_512.png";
        const string MaterialPath = "Assets/Materials/ChainMetal.mat";

        public static void DiagnoseChainAsset()
        {
            var importer = AssetImporter.GetAtPath(LinkPath) as ModelImporter;
            if (importer == null) throw new System.InvalidOperationException("Chain link FBX is missing.");
            importer.useFileScale = false;
            importer.globalScale = .01f;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LinkPath);
            var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
            Debug.Log("CHAIN_ASSET: root=" + model + " rootScale=" +
                (model != null ? model.transform.localScale.ToString("F5") : "none") + " childScale=" +
                (filter != null ? filter.transform.localScale.ToString("F5") : "none") + " mesh=" +
                (filter != null ? filter.sharedMesh.name : "missing") + " bounds=" +
                (filter != null ? filter.sharedMesh.bounds.ToString("F5") : "none") + " childRotation=" +
                (filter != null ? filter.transform.localEulerAngles.ToString() : "none"));
        }

        [MenuItem("Chainbound Raid/Apply Network Chain")]
        public static void Apply()
        {
            Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(LinkPath) as ModelImporter;
            if (importer == null) throw new System.InvalidOperationException("Chain link FBX is missing.");
            if (importer.useFileScale || !Mathf.Approximately(importer.globalScale, .01f))
            {
                importer.useFileScale = false;
                importer.globalScale = .01f;
                importer.SaveAndReimport();
            }
            var link = AssetDatabase.LoadAssetAtPath<GameObject>(LinkPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(TexturePath);
            if (link == null || texture == null) throw new System.InvalidOperationException("Chain link or texture is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new System.InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", .7f);
            material.SetFloat("_Smoothness", .45f);
            EditorUtility.SetDirty(material);
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
            var chain = chainObject.GetComponent<RaidChain>();
            if (chain == null) chain = chainObject.AddComponent<RaidChain>();
            chain.chainLinkPrefab = link;
            chain.chainMaterial = material;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Network raid chain applied to raid scene.");
        }
    }
}
