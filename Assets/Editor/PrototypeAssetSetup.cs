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
        const string PlayerSource = "Assets/ThirdParty/KenneyBlockyCharacters/Models/FBX format/";
        const string BossSource = "Assets/ThirdParty/MammothPrototype/fantasy-mammoth-rigged.fbx";
        const string BossTexture = "Assets/ThirdParty/MammothPrototype/Textures/fantasy_mammoth_3d_model_basecolor.jpg";
        const string BossNormal = "Assets/ThirdParty/MammothPrototype/Textures/fantasy_mammoth_3d_model_normal.png";
        const string Output = "Assets/Materials/TemporaryCharacters/";

        [MenuItem("Chainbound Raid/Apply Temporary Characters")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            ConfigureMammothScale();
            LoopAnimations(BossSource, "Idle", "Walk");
            LoopAnimations(PlayerSource + "character-a.fbx", "idle", "walk", "sprint", "holding-both");
            LoopAnimations(PlayerSource + "character-b.fbx", "idle", "walk", "sprint", "holding-both");
            var playerPrefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/NetworkRaidPlayer.prefab");
            try
            {
                var visual = playerPrefab.GetComponent<RaidPlayerVisual>();
                if (visual == null || visual.modelRoot == null) throw new InvalidOperationException("Player visual is missing.");
                HidePrototypeMeshes(visual.modelRoot, false);
                var a = InstallPlayer(visual.modelRoot, "a");
                var b = InstallPlayer(visual.modelRoot, "b");
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
            Debug.Log("TEMP_ASSETS: player variants and mammoth applied.");
        }

        public static void ApplyAndBuild()
        {
            Apply();
            PrototypeBuild.BuildWindows();
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
            string source = PlayerSource + "character-" + variant + ".fbx";
            var character = Install(source, parent, "Temporary Player " + variant.ToUpperInvariant());
            character.transform.localScale = Vector3.one * .65f;
            character.transform.localPosition = new Vector3(0f, -1f, 0f);
            character.transform.localRotation = Quaternion.identity;
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(PlayerSource + "Textures/texture-" + variant + ".png");
            var material = Material(Output + "Player" + variant.ToUpperInvariant() + ".mat", texture, Color.white);
            foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
            var animator = EnsureAnimator(character);
            string[] clips = { "idle", "walk", "sprint", "attack-melee-right", "attack-melee-left",
                "emote-no", "die", "holding-both" };
            animator.runtimeAnimatorController = Controller(Output + "Player" + variant.ToUpperInvariant() + ".controller",
                source, "idle", clips, clips);
            return character;
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
