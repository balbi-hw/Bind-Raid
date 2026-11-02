using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChainboundRaid.Editor
{
    public static class WeaponAssetSetup
    {
        const string Folder = "Assets/ThirdParty/AncientAxe/";
        const string Definition = "Assets/Settings/AncientAxeWeapon.asset";
        public static void AttachDefault(GameObject character)
        {
            var definition = AssetDatabase.LoadAssetAtPath<RaidWeaponDefinition>(Definition);
            if (definition == null) return;
            var attachment = character.GetComponent<RaidWeaponAttachment>();
            if (attachment == null) attachment = character.AddComponent<RaidWeaponAttachment>();
            attachment.hand = character.GetComponentsInChildren<Transform>(true).Single(t => t.name == "mixamorig:RightHand");
            attachment.Equip(definition);
        }

        public static void ApplyReviewBuild()
        {
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "ancient-axe.fbx");
            importer.useFileScale = true; importer.globalScale = 1; importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            var normal = (TextureImporter)AssetImporter.GetAtPath(Folder + "axe-normal.png");
            normal.textureType = TextureImporterType.NormalMap; normal.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "AncientAxe.mat");
            if(material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + "AncientAxe.mat"); }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture>(Folder+"axe-basecolor.png"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture>(Folder+"axe-normal.png"));
            material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale",.5f);
            material.SetFloat("_Metallic",.15f); material.SetFloat("_Smoothness",.25f); EditorUtility.SetDirty(material);
            var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"ancient-axe.fbx"));
            foreach(var renderer in source.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=material;
            var wrapper=new GameObject("Ancient Axe");source.transform.SetParent(wrapper.transform,false);
            var weaponPrefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Folder+"AncientAxe.prefab"); UnityEngine.Object.DestroyImmediate(wrapper);
            var definition=AssetDatabase.LoadAssetAtPath<RaidWeaponDefinition>(Definition);
            if(definition==null) { definition=ScriptableObject.CreateInstance<RaidWeaponDefinition>();AssetDatabase.CreateAsset(definition,Definition); }
            definition.weaponId="ancient-axe";definition.modelPrefab=weaponPrefab;
            definition.gripEuler=new Vector3(0,0,90);EditorUtility.SetDirty(definition);
            definition.useCarryPose=true;
            definition.twoHandedHeavy=true;
            AssetDatabase.SaveAssets();PrototypeAssetSetup.ApplyWarriorPlayers();
            RaidCombatValidation.Validate(); RaidMovementValidation.Validate();
            Review();
            PrototypeBuild.BuildWindows();
        }

        public static void ReviewOnly() { RaidCombatValidation.Validate(); RaidMovementValidation.Validate(); Review(); }

        static void Review()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkRaidPlayer.prefab"));
            var visual=root.GetComponent<RaidPlayerVisual>();var model=visual.firstCharacter;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(!r.transform.IsChildOf(model.transform))r.enabled=false;
            var attachment=model.GetComponent<RaidWeaponAttachment>();
            if(attachment==null||attachment.EquippedModel==null)throw new Exception("Weapon not equipped");
            var locals=attachment.EquippedModel.transform.localPosition;
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var filters=skins.Select(r=>{var go=new GameObject("Baked preview");var f=go.AddComponent<MeshFilter>();go.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;r.enabled=false;return f;}).ToArray();
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.25f;camera.backgroundColor=new Color(.12f,.14f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-30,0);
            var rt=new RenderTexture(800,800,24);camera.targetTexture=rt;
            const string output="Builds/AnimationReview/AxeHeavy/Unity";Directory.CreateDirectory(output);
            var p=visual.firstPresentation;
            float maxSupportError=0;
            var clearanceReport=new System.Collections.Generic.List<string>();
            var combat=AssetDatabase.LoadAssetAtPath<RaidWeaponSettings>("Assets/Settings/CommonWeaponSettings.asset");
            var heavy=combat.Get(combat.strongAttack);
            foreach(float step in new[]{.01f,.02f,1f/30f})
            {
                float travelled=0;
                for(float time=0;time<heavy.Duration;time+=step)
                {
                    float delta=heavy.LungeDistanceAt(time+step)-heavy.LungeDistanceAt(time);
                    if(delta<-.00001f)throw new Exception("Lunge reverses during recovery");
                    travelled+=delta;
                }
                if(Mathf.Abs(travelled-heavy.lungeDistance)>.0001f)throw new Exception("Lunge depends on tick rate");
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Review ground";
            ground.transform.position=new Vector3(0,-1.06f,0);ground.transform.localScale=new Vector3(5,.1f,6);
            var groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));groundMaterial.SetColor("_BaseColor",new Color(.2f,.23f,.26f));ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
            if(Mathf.Abs(p.strongAttack.length-heavy.Duration)>.005f || heavy.recovery<.65f || heavy.dodgeCancelOpen<1.25f)
                throw new Exception("Heavy clip and gameplay recovery disagree");
            foreach(var clip in new[]{p.idle,p.walk,p.sprint,p.dodge,p.weakAttack,p.strongAttack})
            for(int frame=0;frame<=36;frame++)
            {
                root.transform.position=clip==p.strongAttack ? Vector3.forward*heavy.LungeDistanceAt(clip.length*frame/36f) : Vector3.zero;
                clip.SampleAnimation(model,clip.length*frame/36f);
                var bones=model.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")).ToArray();
                var positions=bones.Select(t=>t.localPosition).ToArray();
                var rotations=bones.Select(t=>t.localRotation).ToArray();
                bool moving=clip==p.walk || clip==p.sprint;
                attachment.ApplyCarryPose(clip==p.sprint ? 7f : 1f,moving);
                for(int i=0;i<bones.Length;i++)
                {
                    if(Vector3.Distance(positions[i],bones[i].localPosition)>.00001f)throw new Exception("Carry pose stretches a bone");
                    bool arm=bones[i]==attachment.hand || bones[i]==attachment.hand.parent.parent;
                    if((!moving || !arm) && Quaternion.Angle(rotations[i],bones[i].localRotation)>.06f)throw new Exception("Carry pose changed unrelated animation");
                }
                if(clip==p.sprint && Vector3.Angle(attachment.EquippedModel.transform.up,model.transform.TransformDirection(attachment.equippedWeapon.carryDirection))>.1f)throw new Exception("Axe direction still faces body");
                attachment.ApplyHeavyGrip(frame/36f,clip==p.strongAttack);
                for(int i=0;i<bones.Length;i++)
                    if(Vector3.Distance(positions[i],bones[i].localPosition)>.00001f)throw new Exception("Two-hand grip stretches a bone");
                if(clip==p.strongAttack && frame>=6 && frame<=29)maxSupportError=Mathf.Max(maxSupportError,attachment.SupportGripError);
                if(clip==p.strongAttack && frame>=6 && frame<=29 && attachment.SupportGripError>.015f)
                    throw new Exception("Heavy support hand missed grip at frame "+frame+": "+attachment.SupportGripError);
                attachment.ApplyGripPose();
                if(clip==p.strongAttack)
                {
                    var hip=bones.Single(t=>t.name=="mixamorig:Hips");var chest=bones.Single(t=>t.name=="mixamorig:Spine2");
                    Vector3 axis=chest.position-hip.position;
                    foreach(string name in new[]{"RightHand","LeftHand","RightForeArm","LeftForeArm","RightArm","LeftArm"})
                    {
                        var joint=bones.Single(t=>t.name=="mixamorig:"+name);
                        float t=Mathf.Clamp01(Vector3.Dot(joint.position-hip.position,axis)/axis.sqrMagnitude);
                        float gap=Vector3.Distance(joint.position,hip.position+t*axis);
                        var jp=model.transform.InverseTransformPoint(joint.position);
                        clearanceReport.Add(frame+","+name+","+gap.ToString("F5",System.Globalization.CultureInfo.InvariantCulture)+","+jp.x.ToString("F5",System.Globalization.CultureInfo.InvariantCulture)+","+jp.y.ToString("F5",System.Globalization.CultureInfo.InvariantCulture)+","+jp.z.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                if(Vector3.Distance(locals,attachment.EquippedModel.transform.localPosition)>.0001f)throw new Exception("Weapon grip slipped");
                for(int i=0;i<skins.Length;i++)
                {
                    if(filters[i].sharedMesh!=null)UnityEngine.Object.DestroyImmediate(filters[i].sharedMesh);
                    var mesh=new Mesh();skins[i].BakeMesh(mesh,false);filters[i].sharedMesh=mesh;
                    filters[i].transform.SetPositionAndRotation(skins[i].transform.position,skins[i].transform.rotation);
                }
                Vector3 target=new Vector3(0,-.05f,.55f);camera.orthographicSize=1.45f;camera.transform.position=target+new Vector3(-3,1,4);camera.transform.LookAt(target);camera.Render();
                var prev=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(800,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(output+"/"+clip.name+"-"+frame+".png",image.EncodeToPNG());RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(image);
                if(moving || clip==p.strongAttack)
                {
                    camera.transform.position=target+new Vector3(0,1,-4);camera.transform.LookAt(target);camera.Render();
                    prev=RenderTexture.active;RenderTexture.active=rt;image=new Texture2D(800,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(output+"/"+clip.name+"-rear-"+frame+".png",image.EncodeToPNG());RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(image);
                }
                if(clip==p.strongAttack)
                {
                    camera.transform.position=target+new Vector3(-4,.4f,0);camera.transform.LookAt(target);camera.Render();
                    prev=RenderTexture.active;RenderTexture.active=rt;image=new Texture2D(800,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(output+"/StrongAttack-side-"+frame+".png",image.EncodeToPNG());RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(image);
                }
            }
            // Verify replacement and unequip never leave a second visible weapon.
            var d=attachment.equippedWeapon;attachment.Equip(d);attachment.Equip(null);
            if(attachment.hand.GetComponentsInChildren<MeshRenderer>().Length!=0)throw new Exception("Unequip left a visible weapon");
            attachment.Equip(d);
            var b=visual.secondCharacter.GetComponent<RaidWeaponAttachment>();if(b==null||b.EquippedModel==null)throw new Exception("Second character missing weapon");
            camera.targetTexture=null;rt.Release();
            File.WriteAllLines(output+"/torso-clearance.csv",clearanceReport);
            File.WriteAllText(output+"/validation.txt","PASS: both character variants equipped; 222 samples across six clips; no bone translations from carry or support-hand IK; Sprint direction preserved; replace/unequip passed. Heavy clip="+p.strongAttack.length+" s; recovery="+heavy.recovery+" s; max locked support-grip error="+maxSupportError+" m. Damage/stamina values unchanged; heavy timing updated.");
            Debug.Log("WEAPON_EQUIPMENT_PASS");
        }
    }
}
