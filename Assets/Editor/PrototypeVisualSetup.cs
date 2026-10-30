using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeVisualSetup
    {
        const string PrefabPath = "Assets/Prefabs/NetworkRaidPlayer.prefab";
        const string ScenePath = "Assets/Scenes/RaidSessionScene.unity";

        [MenuItem("Chainbound Raid/Apply Status Visuals")]
        public static void Apply()
        {
            Directory.CreateDirectory("Assets/Materials");
            Material lit = GetMaterial("Assets/Materials/StatusLit.mat", "Universal Render Pipeline/Lit");
            Material unlit = GetMaterial("Assets/Materials/StatusUnlit.mat", "Universal Render Pipeline/Unlit");
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                SetupPlayer(prefab, lit, unlit);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject boss = GameObject.Find("Boss Training Dummy");
            if (boss == null) throw new System.InvalidOperationException("Boss Training Dummy is missing.");
            SetupBoss(boss, lit, unlit);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Status visuals applied to player prefab and raid scene.");
        }

        static Material GetMaterial(string path, string shaderName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Missing shader: " + shaderName);
            material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetupPlayer(GameObject root, Material lit, Material unlit)
        {
            root.GetComponent<MeshRenderer>().enabled = false;
            Transform model = EnsureChild(root.transform, "Status Model");
            Renderer torso = Shape(model, "Torso", PrimitiveType.Capsule, new Vector3(0, 0, 0), new Vector3(.55f, .55f, .36f), lit);
            Renderer head = Shape(model, "Head", PrimitiveType.Sphere, new Vector3(0, .72f, 0), Vector3.one * .36f, lit);
            Transform leftArm = EnsureChild(model, "Left Arm Pivot");
            leftArm.localPosition = new Vector3(-.42f, .32f, 0);
            Renderer left = Shape(leftArm, "Left Arm", PrimitiveType.Capsule, new Vector3(0, -.25f, 0), new Vector3(.15f, .34f, .15f), lit);
            Transform rightArm = EnsureChild(model, "Right Arm Pivot");
            rightArm.localPosition = new Vector3(.42f, .32f, 0);
            Renderer right = Shape(rightArm, "Right Arm", PrimitiveType.Capsule, new Vector3(0, -.25f, 0), new Vector3(.15f, .34f, .15f), lit);
            Transform weapon = EnsureChild(rightArm, "Weapon Pivot");
            weapon.localPosition = new Vector3(0, -.45f, .14f);
            Shape(weapon, "Practice Blade", PrimitiveType.Cube, new Vector3(0, -.3f, .18f), new Vector3(.13f, .65f, .16f), lit);
            LineRenderer ring = Ring(root.transform, "Player Status Ring", unlit);
            RaidPlayerVisual visual = root.GetComponent<RaidPlayerVisual>();
            if (visual == null) visual = root.AddComponent<RaidPlayerVisual>();
            visual.modelRoot = model;
            visual.leftArm = leftArm;
            visual.rightArm = rightArm;
            visual.weapon = weapon;
            visual.coloredParts = new[] { torso, head, left, right };
            visual.statusRing = ring;
        }

        static void SetupBoss(GameObject root, Material lit, Material unlit)
        {
            root.GetComponent<MeshRenderer>().enabled = false;
            Transform model = EnsureChild(root.transform, "Boss Status Model");
            Renderer body = Shape(model, "Body", PrimitiveType.Cube, Vector3.zero, Vector3.one, lit);
            Renderer trunk = Shape(model, "Trunk Target", PrimitiveType.Capsule,
                RaidBoss.TrunkAnchorLocal, new Vector3(.25f, .38f, .22f), lit);
            Renderer leg = Shape(model, "Front Leg Target", PrimitiveType.Capsule,
                RaidBoss.FrontLegAnchorLocal, new Vector3(.2f, .38f, .2f), lit);
            LineRenderer ring = Ring(root.transform, "Stomp Warning Ring", unlit);
            RaidBossVisual visual = root.GetComponent<RaidBossVisual>();
            if (visual == null) visual = root.AddComponent<RaidBossVisual>();
            visual.bodyRoot = model;
            visual.body = body;
            visual.trunk = trunk;
            visual.frontLeg = leg;
            visual.stompRing = ring;
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            return child;
        }

        static Renderer Shape(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            Transform existing = parent.Find(name);
            GameObject shape;
            if (existing == null)
            {
                shape = GameObject.CreatePrimitive(type);
                shape.name = name;
                shape.transform.SetParent(parent, false);
            }
            else shape = existing.gameObject;
            shape.transform.localPosition = position;
            shape.transform.localRotation = Quaternion.identity;
            shape.transform.localScale = scale;
            Collider collider = shape.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            Renderer renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        static LineRenderer Ring(Transform parent, string name, Material material)
        {
            Transform child = EnsureChild(parent, name);
            LineRenderer line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = .05f;
            return line;
        }
    }
}
