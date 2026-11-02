using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeAssetSetup
    {
        const string WarriorSource = "Assets/ThirdParty/SpartanTripo/spartan-tripo.fbx";
        const string BossSource = "Assets/ThirdParty/MammothPrototype/fantasy-mammoth-rigged.fbx";
        const string BossTexture = "Assets/ThirdParty/MammothPrototype/Textures/fantasy_mammoth_3d_model_basecolor.jpg";
        const string BossNormal = "Assets/ThirdParty/MammothPrototype/Textures/fantasy_mammoth_3d_model_normal.png";
        const string Output = "Assets/Materials/TemporaryCharacters/";

        [MenuItem("Chainbound Raid/Apply Temporary Characters")]
        public static void Apply()
        {
            ApplyWarriorPlayers();
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            ConfigureMammothScale();
            LoopAnimations(BossSource, "Idle", "Walk");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaidSessionScene.unity");
            var boss = GameObject.Find("Boss Training Dummy");
            if (boss == null) throw new InvalidOperationException("Boss is missing.");
            var bossVisual = boss.GetComponent<RaidBossVisual>();
            if (bossVisual == null || bossVisual.bodyRoot == null) throw new InvalidOperationException("Boss visual is missing.");
            if (bossVisual.trunk != null)
                bossVisual.trunk.transform.localPosition = boss.GetComponent<RaidBoss>().TrunkAnchorLocal;
            if (bossVisual.frontLeg != null)
                bossVisual.frontLeg.transform.localPosition = boss.GetComponent<RaidBoss>().FrontLegAnchorLocal;
            HidePrototypeMeshes(bossVisual.bodyRoot, false);
            var oldElephant = bossVisual.bodyRoot.Find("Temporary Elephant");
            if (oldElephant != null) UnityEngine.Object.DestroyImmediate(oldElephant.gameObject);
            var mammoth = Install(BossSource, bossVisual.bodyRoot, "Temporary Mammoth");
            mammoth.transform.localScale = Vector3.one * 3.6f;
            mammoth.transform.localPosition = new Vector3(0f, -.5f, 0f);
            mammoth.transform.localRotation = Quaternion.identity;
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(BossTexture);
            if (texture == null) throw new InvalidOperationException("Mammoth texture is missing.");
            var mammothMaterial = Material(Output + "Mammoth.mat", texture, Color.white);
            var normalImporter = AssetImporter.GetAtPath(BossNormal) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            var normal = AssetDatabase.LoadAssetAtPath<Texture>(BossNormal);
            if (normal != null)
            {
                mammothMaterial.SetTexture("_BumpMap", normal);
                mammothMaterial.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(mammothMaterial);
            }
            foreach (var renderer in mammoth.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = mammothMaterial;
            bossVisual.bossRenderers = mammoth.GetComponentsInChildren<Renderer>(true);
            var bossAnimator = EnsureAnimator(mammoth);
            string[] bossClips = { "Idle", "Walk", "Telegraph", "Stomp", "TrunkSweep", "Charge", "Stagger" };
            bossAnimator.runtimeAnimatorController = Controller(Output + "Mammoth.controller",
                BossSource, "Idle", bossClips, bossClips);
            bossVisual.bossAnimator = bossAnimator;
            bossVisual.presentation = AssetDatabase.LoadAssetAtPath<RaidBossPresentation>("Assets/Settings/MammothPresentation.asset");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TEMP_ASSETS: warrior players and mammoth applied.");
        }

        [MenuItem("Chainbound Raid/Apply Spartan Warrior Players")]
        public static void ApplyWarriorPlayers()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            ConfigureWarriorScale();
            ConfigureTripoTextures();
            LoopAnimations(WarriorSource, "Idle", "Walk", "Sprint", "Brace", "Pull");
            ConfigureWarriorPresentation("A");
            ConfigureWarriorPresentation("B");
            var playerPrefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/NetworkRaidPlayer.prefab");
            try
            {
                var visual = playerPrefab.GetComponent<RaidPlayerVisual>();
                if (visual == null || visual.modelRoot == null) throw new InvalidOperationException("Player visual is missing.");
                HidePrototypeMeshes(visual.modelRoot, false);
                var a = InstallPlayer(visual.modelRoot, "A");
                var b = InstallPlayer(visual.modelRoot, "B");
                visual.firstCharacter = a;
                visual.secondCharacter = b;
                visual.firstAnimator = a.GetComponent<Animator>();
                visual.secondAnimator = b.GetComponent<Animator>();
                visual.firstPresentation = AssetDatabase.LoadAssetAtPath<RaidCharacterPresentation>("Assets/Settings/PlayerAPresentation.asset");
                visual.secondPresentation = AssetDatabase.LoadAssetAtPath<RaidCharacterPresentation>("Assets/Settings/PlayerBPresentation.asset");
                b.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(playerPrefab, "Assets/Prefabs/NetworkRaidPlayer.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(playerPrefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("WARRIOR_ASSETS: player variants applied.");
        }

        public static void ApplyAndBuild()
        {
            Apply();
            PrototypeBuild.BuildWindows();
        }

        public static void ApplySpartanAndBuild()
        {
            ApplyWarriorPlayers();
            ValidateSpartan();
            PrototypeBuild.BuildWindows();
        }

        public static void ValidateSpartan()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorSource);
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                int clips = 0;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(WarriorSource))
                {
                    if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                    clips++;
                    for (int frame = 0; frame <= 4; frame++)
                    {
                        clip.SampleAnimation(instance, clip.length * frame / 4f);
                        foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var baked = new Mesh();
                            skin.BakeMesh(baked);
                            var size = baked.bounds.size;
                            UnityEngine.Object.DestroyImmediate(baked);
                            if (float.IsNaN(size.x) || float.IsNaN(size.y) || float.IsNaN(size.z) || size.magnitude > 10f)
                                throw new InvalidOperationException("Invalid Spartan deformation: " + clip.name);
                        }
                    }
                }
                if (clips != 14) throw new InvalidOperationException("Expected 14 Spartan clips, got " + clips);
                Debug.Log("SPARTAN_VALIDATION: 14 clips sampled, skinned bounds finite.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        public static void ApplyBossChainAndBuild()
        {
            Apply();
            PrototypeChainSetup.Apply();
            PrototypeBuild.BuildWindows();
        }

        public static void ApplyDiagnoseAndBuild()
        {
            Apply();
            DiagnoseBoss();
            PrototypeBuild.BuildWindows();
        }

        public static void DiagnoseBoss()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/RaidSessionScene.unity");
            var boss = GameObject.Find("Boss Training Dummy");
            var model = boss != null ? boss.transform.Find("Boss Status Model/Temporary Mammoth") : null;
            Debug.Log("MAMMOTH_DIAG: boss=" + (boss != null) + " model=" + (model != null));
            if (model == null) return;
            Debug.Log("MAMMOTH_DIAG: root position=" + boss.transform.position +
                " scale=" + boss.transform.lossyScale + " model scale=" + model.lossyScale +
                " active=" + model.gameObject.activeInHierarchy);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Debug.Log("MAMMOTH_DIAG: renderer=" + renderer.name + " enabled=" + renderer.enabled +
                    " bounds=" + renderer.bounds + " localBounds=" +
                    (renderer is SkinnedMeshRenderer skin ? skin.localBounds.ToString() : "n/a") +
                    " transform=" + renderer.transform.localPosition + " rotation=" + renderer.transform.localEulerAngles + " material=" +
                    (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null"));
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                    foreach (var bone in skinnedRenderer.bones)
                        if (bone.name == "Head" || bone.name == "Trunk" || bone.name.StartsWith("Front", StringComparison.Ordinal))
                            Debug.Log("MAMMOTH_BONE: " + bone.name + " position=" + bone.position);
            }
        }

        public static void DiagnoseBossClips()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(BossSource))
                if (asset is AnimationClip clip)
                    Debug.Log("MAMMOTH_CLIP: " + clip.name + " length=" + clip.length);
        }

        static GameObject InstallPlayer(Transform parent, string variant)
        {
            var character = Install(WarriorSource, parent, "Temporary Player " + variant);
            character.transform.localScale = Vector3.one;
            character.transform.localPosition = new Vector3(0f, -1f, 0f);
            character.transform.localRotation = Quaternion.identity;
            foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                // The source skin uses four influences. Low quality tiers otherwise truncate them.
                if (renderer is SkinnedMeshRenderer skin) skin.quality = SkinQuality.Bone4;
                string part = renderer.name;
                var texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/ThirdParty/SpartanTripo/Textures/" + part + "_basecolor.png");
                if (texture == null) throw new InvalidOperationException("Missing Tripo texture: " + part);
                // Keep skin and armor colors; only the cloth distinguishes the two players.
                Color tint = variant == "B" && part == "tripo_part_0" ? new Color(.55f, .7f, 1f) : Color.white;
                var material = Material(Output + "Tripo_" + part + "_" + variant + ".mat", texture, tint);
                material.shader = Shader.Find("Universal Render Pipeline/Lit");
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
                string normalPath = "Assets/ThirdParty/SpartanTripo/Textures/" + part + "_normal.png";
                var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
                {
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    normalImporter.SaveAndReimport();
                }
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>(normalPath));
                material.DisableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 0f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", .1f);
                // The supplied GLB renders both sides, including its thin cape/skirt surfaces.
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                EditorUtility.SetDirty(material);
                renderer.sharedMaterial = material;
            }
            var animator = EnsureAnimator(character);
            string[] clips = { "Idle", "Walk", "Sprint", "WeakAttack", "StrongAttack", "Dodge",
                "HitReaction", "Knockdown", "Incapacitated", "Grabbed", "GetUp", "Brace", "Pull", "Rescue" };
            animator.runtimeAnimatorController = Controller(Output + "Player" + variant + ".controller",
                WarriorSource, "Idle", clips, clips);
            WeaponAssetSetup.AttachDefault(character);
            return character;
        }

        static void ConfigureWarriorPresentation(string variant)
        {
            var profile = AssetDatabase.LoadAssetAtPath<RaidCharacterPresentation>(
                "Assets/Settings/Player" + variant + "Presentation.asset");
            if (profile == null) throw new InvalidOperationException("Missing player presentation " + variant);
            AnimationClip Clip(string name)
            {
                var clip = Array.Find(AssetDatabase.LoadAllAssetsAtPath(WarriorSource), asset =>
                    asset is AnimationClip animation &&
                    (animation.name == name || animation.name.EndsWith("|" + name, StringComparison.Ordinal)) &&
                    !animation.name.StartsWith("__preview__", StringComparison.Ordinal)) as AnimationClip;
                if (clip == null) throw new InvalidOperationException("Missing warrior clip " + name);
                return clip;
            }
            profile.idle = Clip("Idle"); profile.walk = Clip("Walk"); profile.sprint = Clip("Sprint");
            profile.weakAttack = Clip("WeakAttack"); profile.strongAttack = Clip("StrongAttack");
            profile.dodge = Clip("Dodge"); profile.hitReaction = Clip("HitReaction");
            profile.knockdown = Clip("Knockdown"); profile.incapacitated = Clip("Incapacitated");
            profile.grabbed = Clip("Grabbed"); profile.getUp = Clip("GetUp");
            profile.brace = Clip("Brace"); profile.pull = Clip("Pull"); profile.rescue = Clip("Rescue");
            profile.proceduralFallback = false;
            // Measured backward foot speed during planted portions of the supplied clips.
            // These describe the motion clips, not the controller's configured top speeds.
            profile.walkReferenceSpeed = 1.0054f;
            profile.sprintReferenceSpeed = 3.2441f;
            EditorUtility.SetDirty(profile);
        }

        static void ConfigureWarriorScale()
        {
            var importer = AssetImporter.GetAtPath(WarriorSource) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Warrior importer is missing.");
            if (importer.useFileScale && Mathf.Approximately(importer.globalScale, 1f) && importer.importNormals == ModelImporterNormals.Calculate) return;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalSmoothingAngle = 120f;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
        }

        static void ConfigureTripoTextures()
        {
            foreach (string path in Directory.GetFiles("Assets/ThirdParty/SpartanTripo/Textures", "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (!importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed) continue;
                // Tripo's dense per-face atlas has no safe mip padding: filtering lower mips
                // blends unrelated UV islands and makes the surface look torn.
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        static GameObject Install(string source, Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (asset == null) throw new InvalidOperationException("Missing model: " + source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        static void HidePrototypeMeshes(Transform parent, bool visible)
        {
            foreach (var renderer in parent.GetComponentsInChildren<MeshRenderer>(true))
                renderer.enabled = visible;
        }

        static Animator EnsureAnimator(GameObject root)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;
            return animator;
        }

        static Material Material(string path, Texture texture, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        static RuntimeAnimatorController Controller(string path, string source, string initial,
            string[] sourceNames, string[] stateNames)
        {
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            AnimationClip initialClip = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(source);
            for (int i = 0; i < sourceNames.Length; i++)
            {
                AnimationClip clip = Array.Find(assets, asset => asset is AnimationClip animation &&
                    (animation.name == sourceNames[i] ||
                     animation.name.EndsWith("|" + sourceNames[i], StringComparison.Ordinal)) &&
                    !animation.name.StartsWith("__preview__", StringComparison.Ordinal)) as AnimationClip;
                if (clip == null) throw new InvalidOperationException("Missing animation " + sourceNames[i] + " in " + source);
                var state = machine.AddState(stateNames[i]);
                state.motion = clip;
                if (sourceNames[i] == initial) { machine.defaultState = state; initialClip = clip; }
            }
            if (initialClip == null) throw new InvalidOperationException("Missing default animation.");
            return controller;
        }

        static void LoopAnimations(string path, params string[] loopNames)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing model importer: " + path);
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            bool changed = false;
            foreach (var clip in clips)
            {
                var sourceClip = Array.Find(importer.defaultClipAnimations, item => item.takeName == clip.takeName);
                if (sourceClip != null && (clip.firstFrame != sourceClip.firstFrame || clip.lastFrame != sourceClip.lastFrame))
                {
                    clip.firstFrame = sourceClip.firstFrame;
                    clip.lastFrame = sourceClip.lastFrame;
                    changed = true;
                }
                bool loop = Array.Exists(loopNames, name => clip.name == name ||
                    clip.name.EndsWith("|" + name, StringComparison.Ordinal));
                if (clip.loopTime == loop) continue;
                clip.loopTime = loop;
                changed = true;
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureMammothScale()
        {
            var importer = AssetImporter.GetAtPath(BossSource) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Mammoth importer is missing.");
            if (!importer.useFileScale && Mathf.Approximately(importer.globalScale, .01f)) return;
            importer.useFileScale = false;
            importer.globalScale = .01f;
            importer.SaveAndReimport();
        }
    }
}
