using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeArenaSetup
    {
        const string ScenePath = "Assets/Scenes/RaidSessionScene.unity";
        const string Output = "Assets/Environment/PrototypeArena/";

        [MenuItem("Chainbound Raid/Apply Ruined Arena Blockout")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var old = GameObject.Find("Arena Floor");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            old = GameObject.Find("Ruined Arena");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            var root = new GameObject("Ruined Arena").transform;
            var floor = new GameObject("Arena Floor");
            floor.transform.SetParent(root, false);
            var mesh = EllipseMesh(20f, 17f, 64);
            string meshPath = Output + "ArenaFloor.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = Material("SandstoneFloor", new Color(.34f, .29f, .24f));
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;

            var darkStone = Material("DarkStone", new Color(.19f, .18f, .19f));
            var paleStone = Material("PaleStone", new Color(.49f, .42f, .34f));
            var accent = Material("WeatheredGold", new Color(.46f, .30f, .14f));
            // Each ring rises and recedes outward like a colosseum grandstand.
            // The outer screen is high enough to hide terrain beyond the arena.
            for (int tier = 0; tier < 6; tier++)
            {
                float radiusX = 20.3f + tier * 1.7f;
                float radiusZ = 17.3f + tier * 1.7f;
                float height = 2.4f + tier * 1.7f;
                var ring = new GameObject("Grandstand Tier " + (tier + 1)).transform;
                ring.SetParent(root, false);
                for (int i = 0; i < 32; i++)
                {
                    float a = (i + .5f) * Mathf.PI * 2f / 32f;
                    float x = Mathf.Cos(a) * radiusX;
                    float z = Mathf.Sin(a) * radiusZ;
                    float span = 2f * Mathf.Sin(Mathf.PI / 32f) *
                        new Vector2(radiusX * Mathf.Sin(a), radiusZ * Mathf.Cos(a)).magnitude * 1.12f;
                    var section = Block("Stone Section " + i.ToString("00"), ring,
                        new Vector3(x, height * .5f, z), new Vector3(span, height, 1.85f),
                        tier % 2 == 0 ? darkStone : paleStone, tier == 0);
                    Vector3 outward = new Vector3(Mathf.Cos(a) / radiusX, 0f,
                        Mathf.Sin(a) / radiusZ).normalized;
                    section.transform.rotation = Quaternion.LookRotation(outward);
                    Block("Seat Ledge", section.transform, new Vector3(0f, .51f, -.05f),
                        new Vector3(.98f, .035f, .92f), tier % 2 == 0 ? paleStone : darkStone, false);
                    if (tier == 5)
                        Block("Outer Screen Wall", section.transform, new Vector3(0f, .65f, .23f),
                            new Vector3(.98f, .24f, .65f), darkStone, false);
                }
            }

            // Four cover/pillar sets around the boss; keep the spawn route clear.
            Cover(root, "Low Cover 11", new Vector3(-9.5f, 0f, 1f), -18f, darkStone, paleStone);
            Cover(root, "Low Cover 01", new Vector3(9.5f, 0f, 1f), 18f, darkStone, paleStone);
            Pillar(root, "Pillar 11", new Vector3(-12f, 0f, 8.5f), darkStone, paleStone, accent);
            Pillar(root, "Pillar 01", new Vector3(12f, 0f, 8.5f), darkStone, paleStone, accent);
            Cover(root, "Low Cover 07", new Vector3(-9.5f, 0f, -9.5f), 18f, darkStone, paleStone);
            Cover(root, "Low Cover 05", new Vector3(9.5f, 0f, -9.5f), -18f, darkStone, paleStone);
            Pillar(root, "Pillar 07", new Vector3(-12f, 0f, -11.5f), darkStone, paleStone, accent);
            Pillar(root, "Pillar 05", new Vector3(12f, 0f, -11.5f), darkStone, paleStone, accent);

            // Ground markings are visual only; they never become chain anchors.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                float radius = i % 2 == 0 ? 11.5f : 14.8f;
                var tile = Block("Sunken Ring Stone " + i.ToString("00"), root,
                    new Vector3(Mathf.Sin(angle) * radius, .025f, Mathf.Cos(angle) * radius),
                    new Vector3(1.15f, .05f, .52f), i % 3 == 0 ? accent : paleStone, false);
                tile.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ARENA_BLOCKOUT: oval 40x34 m, 6 rising grandstand tiers, 4 covers, 4 pillar placeholders.");
        }

        public static void ApplyAndBuild()
        {
            Apply();
            PrototypeBuild.BuildWindows();
        }

        static Mesh EllipseMesh(float radiusX, float radiusZ, int segments)
        {
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radiusX, 0f, Mathf.Sin(a) * radiusZ);
                uv[i + 1] = new Vector2((Mathf.Cos(a) + 1f) * .5f, (Mathf.Sin(a) + 1f) * .5f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Prototype Oval Arena", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material Material(string name, Color color)
        {
            string path = Output + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .16f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Block(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collision)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            return block;
        }

        static void Cover(Transform root, string name, Vector3 position, float angle, Material dark, Material pale)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            group.localPosition = position;
            group.localRotation = Quaternion.Euler(0f, angle, 0f);
            Block("Collapsed Foundation", group, new Vector3(0f, .55f, 0f),
                new Vector3(4.2f, 1.1f, 1.5f), dark, true);
            Block("Broken Slab A", group, new Vector3(-1.15f, 1.18f, 0f),
                new Vector3(1.8f, .28f, 1.6f), pale, false).transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            Block("Broken Slab B", group, new Vector3(1.25f, 1.12f, .1f),
                new Vector3(1.5f, .25f, 1.4f), pale, false).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        }

        static void Pillar(Transform root, string name, Vector3 position, Material dark, Material pale, Material accent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            group.localPosition = position;
            Block("Base", group, new Vector3(0f, .25f, 0f), new Vector3(2f, .5f, 2f), dark, false);
            Block("Shaft", group, new Vector3(0f, 1.95f, 0f), new Vector3(1.15f, 3f, 1.15f), pale, false);
            Block("Fracture Band", group, new Vector3(0f, 2.45f, 0f),
                new Vector3(1.25f, .12f, 1.25f), accent, false).transform.localRotation = Quaternion.Euler(0f, 0f, 7f);
            Block("Broken Crown", group, new Vector3(0f, 3.45f, 0f),
                new Vector3(1.6f, .45f, 1.6f), dark, false).transform.localRotation = Quaternion.Euler(0f, 0f, -11f);
        }
    }
}
