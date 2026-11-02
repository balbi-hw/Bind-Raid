using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class LocomotionValidation
    {
        static void Require(bool condition,string message) {if(!condition)throw new Exception(message);}
        public static void ApplyValidateBuild()
        {
            PrototypeAssetSetup.ApplyWarriorPlayers();
            RaidMovementValidation.Validate();
            var report=new List<string>();
            foreach(int fps in new[]{30,60,144,240})
            {
                var motion=new RaidPresentationMotion();Vector3 root=Vector3.zero;
                Vector3 lastRoot=root;float nextFixed=.02f;float maxError=0,oldSpeed=0,maxRotationError=0;int count=0;
                Quaternion rotation=Quaternion.identity;
                for(int frame=1;frame<=fps*4;frame++)
                {
                    float t=(float)frame/fps;
                    while(nextFixed<=t+1e-6f)
                    {
                        var before=root;root+=Vector3.forward*.1f;
                        motion.Record(before,root,.02f,nextFixed);nextFixed+=.02f;
                        Quaternion rotationBefore=rotation;
                        rotation=Quaternion.Euler(0,90f*(nextFixed-.02f),0);
                        motion.RecordRotation(rotationBefore,rotation);
                    }
                    if(t>1)
                    {
                        var shown=root+motion.Offset(root,t);
                        maxError=Mathf.Max(maxError,Mathf.Abs(shown.z-5f*(t-.02f)));
                        oldSpeed+=Mathf.Min((root-lastRoot).magnitude*fps,8f);count++;
                        Require(Mathf.Abs(motion.Speed-5f)<.0001f,"Measured speed varies with rendering rate");
                        maxRotationError=Mathf.Max(maxRotationError,Quaternion.Angle(
                            motion.Rotation(rotation,t),Quaternion.Euler(0,90f*(t-.02f),0)));
                    }
                    lastRoot=root;
                }
                Require(maxError<.001f,"Visual motion is stepping at "+fps+" fps");
                Require(maxRotationError<.06f,"Visual rotation is stepping at "+fps+" fps");
                report.Add(fps+" fps: turn interpolation max error="+maxRotationError.ToString("F5")+" degrees");
                report.Add(fps+" fps: old measured speed="+(oldSpeed/count).ToString("F3")+" m/s; new=5.000; max position error="+maxError.ToString("F6")+" m");
                motion.Record(root,root+Vector3.up*.2f,.02f,4);
                Require(motion.Speed==0,"Vertical motion affects gait speed");
                motion.Rebase(Vector3.one*100);
                Require(motion.Offset(Vector3.one*100,4.01f).magnitude<.21f,"Reconciliation creates interpolation teleport");
                var rotationOffset=Quaternion.Inverse(rotation)*motion.Rotation(rotation,4.01f);
                var rebasedRotation=Quaternion.Euler(0,-120,0);
                motion.RebaseRotation(rebasedRotation);
                Require(Quaternion.Angle(Quaternion.Inverse(rebasedRotation)*motion.Rotation(rebasedRotation,4.01f),rotationOffset)<.06f,"Rebase changes rotation offset");
                motion.Reset();Require(motion.Offset(Vector3.one*200,5)==Vector3.zero && motion.Speed==0,"Teleport retains old motion");
                Require(Quaternion.Angle(motion.Rotation(rebasedRotation,5),rebasedRotation)<.06f,"Teleport retains old rotation");
            }
            var profile=AssetDatabase.LoadAssetAtPath<RaidCharacterPresentation>("Assets/Settings/PlayerAPresentation.asset");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/SpartanTripo/spartan-tripo.fbx");
            Dictionary<string,Quaternion> reference=null;
            foreach(int fps in new[]{30,60,144})
            {
                var root=UnityEngine.Object.Instantiate(source);
                try
                {
                    var animator=root.GetComponent<Animator>();
                    if(animator==null)animator=root.AddComponent<Animator>();
                    using(var driver=new RaidAnimationDriver(animator,profile))
                        for(int f=0;f<fps*2;f++)driver.Evaluate(7,null,0,0,false,1f/fps);
                    var pose=root.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("mixamorig")).ToDictionary(t=>t.name,t=>t.localRotation);
                    if(reference==null)reference=pose;
                    else foreach(var item in pose)Require(Quaternion.Angle(reference[item.Key],item.Value)<.1f,"Gait phase differs by frame rate: "+item.Key);
                }
                finally {UnityEngine.Object.DestroyImmediate(root);}
            }
            var sample=UnityEngine.Object.Instantiate(source);
            try
            {
                foreach(var clip in new[]{profile.walk,profile.sprint})
                {
                    var bones=sample.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("mixamorig")).ToArray();
                    clip.SampleAnimation(sample,0);var first=bones.Select(b=>b.position).ToArray();
                    clip.SampleAnimation(sample,clip.length-.00001f);
                    float gap=bones.Select((b,i)=>Vector3.Distance(first[i],b.position)).Max();
                    Require(gap<.01f,"Imported locomotion loop pops: "+clip.name+" "+gap);
                    report.Add(clip.name+" imported loop gap="+gap.ToString("F6")+" m");
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(sample);}
            report.Add("PASS: gait phase invariant at 30/60/144 fps; movement motor regression passed. Controller speeds unchanged (5/7 m/s).");
            Directory.CreateDirectory("Builds/AnimationReview/LocomotionFix");
            File.WriteAllLines("Builds/AnimationReview/LocomotionFix/validation.txt",report);
            Debug.Log("LOCOMOTION_VALIDATION_PASS\n"+string.Join("\n",report));
            PrototypeBuild.BuildWindows();
        }
    }
}
