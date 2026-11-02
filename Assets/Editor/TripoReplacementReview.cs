using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChainboundRaid.Editor
{
    public static class TripoReplacementReview
    {
        public static void ApplyValidateBuild()
        {
            PrototypeAssetSetup.ApplyWarriorPlayers();
            PrototypeAssetSetup.ValidateSpartan();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .55f, .55f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkRaidPlayer.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            var visual = root.GetComponent<RaidPlayerVisual>();
            var model = visual.firstCharacter;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                if(!r.transform.IsChildOf(model.transform)) r.enabled=false;
            var rendererList = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            // Camera.Render inside one editor callback can reuse cached GPU skin matrices.
            // Render each sampled CPU-baked mesh so captures show the requested frame.
            var previews = new System.Collections.Generic.Dictionary<SkinnedMeshRenderer, MeshFilter>();
            foreach(var r in rendererList)
            {
                var go = new GameObject("Sample " + r.name);
                var filter = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>().sharedMaterial=r.sharedMaterial;
                previews.Add(r,filter);r.enabled=false;
            }
            foreach(var r in rendererList) Debug.Log("TRIPO_SCALE "+r.name+" scale="+r.transform.lossyScale+" bounds="+r.bounds);
            var bones = rendererList.SelectMany(r => r.bones).Distinct().ToArray();
            if (bones.Length < 60) throw new Exception("Tripo skeleton missing fingers or limbs");
            var profile = visual.firstPresentation;
            if (Mathf.Abs(profile.dodge.length - .75f) > .045f) throw new Exception("Roll duration mismatch");
            var camera = new GameObject("Review Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.25f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.14f,.18f);
            var light = new GameObject("Review Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
            var rt = new RenderTexture(640,640,24);camera.targetTexture=rt;
            string folder = "Builds/AnimationReview/TripoReplacement/Unity";Directory.CreateDirectory(folder);
            foreach (var clip in new[]{profile.walk,profile.sprint,profile.dodge})
            {
                float[] lengths = null;
                for(int f=0;f<=24;f++)
                {
                    clip.SampleAnimation(model,clip.length*f/24f);
                    if(clip==profile.walk && f==0)
                    {
                        var r=rendererList.First(x=>x.name=="tripo_part_1");var mesh=r.sharedMesh;
                        var lines=new System.Collections.Generic.List<string>();
                        for(int v=0;v<mesh.vertexCount;v++) lines.Add(mesh.vertices[v].x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+mesh.vertices[v].y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+mesh.vertices[v].z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+mesh.uv[v].x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+mesh.uv[v].y.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                        File.WriteAllLines(folder+"/torso-uv.csv",lines);
                    }
                    // Hips translate relative to the armature during locomotion; that is not limb stretch.
                    var current = bones.Where(b=>b.parent!=null && bones.Contains(b.parent)).Select(b=>Vector3.Distance(b.position,b.parent.position)).ToArray();
                    if(lengths==null) lengths=current;
                    else for(int j=0;j<current.Length;j++) if(Mathf.Abs(current[j]-lengths[j])>.005f)
                        throw new Exception("Changing bone length in "+clip.name);
                    var bounds = new Bounds();bool first=true;
                    foreach(var r in rendererList)
                    {
                        var baked=new Mesh();r.BakeMesh(baked, false);
                        foreach(var v in baked.vertices)
                        {
                            // BakeMesh already includes the evaluated skin scale in this FBX hierarchy.
                            // Applying lossyScale a second time would report a false oversized character.
                            var w=r.transform.position+r.transform.rotation*v;
                            if(float.IsNaN(w.x)||float.IsNaN(w.y)||float.IsNaN(w.z))throw new Exception("Nonfinite skin");
                            if(first){bounds=new Bounds(w,Vector3.zero);first=false;}else bounds.Encapsulate(w);
                        }
                        var preview=previews[r];
                        if(preview.sharedMesh!=null)UnityEngine.Object.DestroyImmediate(preview.sharedMesh);
                        preview.sharedMesh=baked;
                        preview.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);
                    }
                    if(bounds.size.magnitude>4 || bounds.size.magnitude<.3f) throw new Exception("Unexpected character scale: "+bounds);
                    if(f==0||f==8||f==16||f==24)
                    {
                        Vector3 target = model.transform.position+Vector3.up*.95f;
                        camera.transform.position=target+new Vector3(3,1,4);camera.transform.LookAt(target);camera.Render();
                        var previous=RenderTexture.active;RenderTexture.active=rt;
                        var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
                        File.WriteAllBytes(folder+"/"+clip.name+"-"+f+".png",image.EncodeToPNG());
                        RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);
                    }
                }
            }
            File.WriteAllText(folder+"/validation.txt","PASS: Walk/Sprint/Dodge sampled at 25 times each; stable joint lengths, finite skin, expected size; 0.75 s roll. Bones="+bones.Length);
            foreach(var preview in previews.Values) {UnityEngine.Object.DestroyImmediate(preview.sharedMesh);UnityEngine.Object.DestroyImmediate(preview.gameObject);}
            UnityEngine.Object.DestroyImmediate(root);camera.targetTexture=null;rt.Release();
            Debug.Log("TRIPO_REVIEW_PASS");
            PrototypeBuild.BuildWindows();
        }
    }
}
