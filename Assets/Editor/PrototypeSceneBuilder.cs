using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace ChainboundRaid.Editor
{
    public static class PrototypeSceneBuilder
    {
        [MenuItem("Chainbound Raid/Create 01-02 Network Scene")]
        public static void CreateNetworkScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Arena Floor";
            floor.transform.localScale = new Vector3(4, 1, 4);
            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);

            var bossObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossObject.name = "Boss Training Dummy";
            bossObject.transform.position = new Vector3(0, 2, 5);
            bossObject.transform.localScale = new Vector3(3, 4, 3);
            bossObject.AddComponent<NetworkObject>();
            bossObject.AddComponent<RaidBoss>();
            var sessionObject = new GameObject("Raid Session");
            sessionObject.AddComponent<NetworkObject>();
            sessionObject.AddComponent<RaidSession>();

            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            var playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Network Raid Player";
            Object.DestroyImmediate(playerObject.GetComponent<CapsuleCollider>());
            playerObject.AddComponent<CharacterController>();
            playerObject.AddComponent<NetworkObject>();
            playerObject.AddComponent<NetworkTransform>();
            playerObject.AddComponent<RaidPlayer>();
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerObject, "Assets/Prefabs/NetworkRaidPlayer.prefab");
            Object.DestroyImmediate(playerObject);

            var networkObject = new GameObject("Network Manager");
            var networkManager = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkObject.AddComponent<RaidBootstrap>();
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<PrototypeCamera>();
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/RaidSessionScene.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/RaidSessionScene.unity", true) };
            AssetDatabase.SaveAssets();
            PrototypeVisualSetup.Apply();
        }

        [MenuItem("Chainbound Raid/Create Prototype Scene")]
        public static void Create()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PrototypeURP.asset");
            if (pipeline == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Settings");
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Settings/PrototypeRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, "Assets/Settings/PrototypeURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Arena Floor";
            floor.transform.localScale = new Vector3(4, 1, 4);
            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);
            var bossObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossObject.name = "Elephant Boss Placeholder";
            bossObject.transform.position = new Vector3(0, 2, 7);
            bossObject.transform.localScale = new Vector3(3, 4, 5);
            var boss = bossObject.AddComponent<ElephantBossPrototype>();
            var one = MakePlayer("Player One", new Vector3(-2, 1, -4));
            var two = MakePlayer("Player Two", new Vector3(2, 1, -4));
            two.forward = KeyCode.UpArrow; two.back = KeyCode.DownArrow;
            two.left = KeyCode.LeftArrow; two.right = KeyCode.RightArrow;
            two.dodge = KeyCode.RightControl; two.brace = KeyCode.RightShift;
            two.pull = KeyCode.Return; two.attack = KeyCode.RightAlt;
            var chainObject = new GameObject("Hybrid Chain");
            var line = chainObject.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.12f;
            line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var chain = chainObject.AddComponent<HybridChain>();
            chain.first = one; chain.second = two; chain.visual = line;
            one.boss = boss; two.boss = boss;
            boss.first = one; boss.second = two; boss.chain = chain;
            var camObject = new GameObject("Main Camera");
            camObject.tag = "MainCamera";
            camObject.AddComponent<Camera>();
            camObject.AddComponent<AudioListener>();
            camObject.AddComponent<PrototypeCamera>().target = one.transform;
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/PrototypeArena.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/PrototypeArena.unity", true) };
            AssetDatabase.SaveAssets();
        }

        static PrototypePlayer MakePlayer(string name, Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            obj.name = name;
            obj.transform.position = position;
            Object.DestroyImmediate(obj.GetComponent<CapsuleCollider>());
            var controller = obj.AddComponent<CharacterController>();
            controller.height = 2f;
            return obj.AddComponent<PrototypePlayer>();
        }
    }
}
