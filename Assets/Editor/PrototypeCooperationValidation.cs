using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeCooperationValidation
    {
        [MenuItem("Chainbound Raid/Validate Cooperation Rules")]
        public static void Validate()
        {
            for (int i = 0; i < 100; i++)
            {
                Check(Mathf.Approximately(RaidCooperationRules.Angle(Vector3.zero,
                    Vector3.right, Vector3.forward), 90f), "right angle");
                Check(Mathf.Approximately(RaidCooperationRules.Angle(Vector3.zero,
                    Vector3.right, Vector3.left), 180f), "opposite angle");
                var valid = RaidCooperationRules.Evaluate(true, true, true, true, true, true,
                    true, true, 120f, 80f, 160f, true, false);
                Check(valid == PullFailure.None, "valid pull");
                Check(RaidCooperationRules.Evaluate(false, true, true, true, true, true,
                    true, true, 120f, 80f, 160f, true, false).HasFlag(PullFailure.NotBraced), "stance");
                Check(RaidCooperationRules.Evaluate(true, true, false, true, true, true,
                    true, true, 120f, 80f, 160f, true, false).HasFlag(PullFailure.NotGrounded), "ground");
                Check(RaidCooperationRules.Evaluate(true, true, true, true, true, true,
                    true, true, 79.9f, 80f, 160f, true, false).HasFlag(PullFailure.InvalidAngle), "angle");
                Check(RaidCooperationRules.Evaluate(true, true, true, true, true, true,
                    true, false, 120f, 80f, 160f, true, false).HasFlag(PullFailure.NoSharedHold), "hold");
                Check(RaidCooperationRules.Evaluate(true, true, true, true, true, true,
                    true, true, 120f, 80f, 160f, false, false).HasFlag(PullFailure.NoAnchor), "anchor");
                Check(RaidCooperationRules.Evaluate(true, true, true, true, true, true,
                    true, true, 120f, 80f, 160f, true, true).HasFlag(PullFailure.PartImmune), "immunity");
                Check(RaidCooperationRules.Evaluate(true, true, true, true, false, true,
                    true, true, 120f, 80f, 160f, true, false).HasFlag(PullFailure.PlayerDisabled), "disabled");
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaidSessionScene.unity");
            var cooperation = GameObject.Find("Raid Cooperation")?.GetComponent<RaidCooperation>();
            Check(cooperation != null && cooperation.settings != null, "scene configuration");
            Debug.Log("COOP_VALIDATION: 100 rule iterations passed; scene=" + scene.name);
        }

        static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Cooperation validation failed: " + label);
        }
    }
}
