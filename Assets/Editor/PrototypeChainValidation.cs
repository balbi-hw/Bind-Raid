using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class PrototypeChainValidation
    {
        [MenuItem("Chainbound Raid/Validate Chain Rules")]
        public static void Validate()
        {
            for (int i = 0; i < 100; i++)
            {
                Check(RaidChainRules.Band(6.99f, 7f, 8f, 9f) == RaidChainBand.Slack, "6.99");
                Check(RaidChainRules.Band(7f, 7f, 8f, 9f) == RaidChainBand.Taut, "7");
                Check(RaidChainRules.Band(8f, 7f, 8f, 9f) == RaidChainBand.Taut, "8");
                Check(RaidChainRules.Band(8.01f, 7f, 8f, 9f) == RaidChainBand.Buffered, "8.01");
                Check(RaidChainRules.Band(9f, 7f, 8f, 9f) == RaidChainBand.Buffered, "9");
                Check(RaidChainRules.Band(9.01f, 7f, 8f, 9f) == RaidChainBand.Overstretched, "9.01");
                Check(RaidChainRules.Display(RaidChainBand.Overstretched, true, true, false, false) ==
                    RaidChainState.Snatched, "snatch priority");
                Check(RaidChainRules.Display(RaidChainBand.Overstretched, true, true, false, true) ==
                    RaidChainState.Down, "down priority");
                Check(Mathf.Approximately(RaidChainRules.Length(Vector3.zero, Vector3.right * 8f, null), 8f),
                    "direct path");
                Check(Mathf.Approximately(RaidChainRules.Length(Vector3.zero, Vector3.right * 8f,
                    Vector3.up * 3f), 3f + Mathf.Sqrt(73f)), "anchored path");
                Vector3 buffered = RaidChainRules.ConstrainDisplacement(Vector3.zero, Vector3.right * 8.5f,
                    null, false, Vector3.right * .2f, 8f, 9f, .4f, out bool bufferAttempt);
                Check(buffered.x < .2f && buffered.x > 0f && !bufferAttempt, "buffer slowing");
                Vector3 blocked = RaidChainRules.ConstrainDisplacement(Vector3.zero, Vector3.right * 9f,
                    null, false, Vector3.right * .2f, 8f, 9f, .4f, out bool hardAttempt);
                Check(blocked == Vector3.zero && hardAttempt, "hard limit");
                Vector3 inward = RaidChainRules.ConstrainDisplacement(Vector3.zero, Vector3.right * 9f,
                    null, false, Vector3.left * .2f, 8f, 9f, .4f, out bool inwardAttempt);
                Check(inward == Vector3.left * .2f && !inwardAttempt, "inward escape");
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaidSessionScene.unity");
            var chain = GameObject.Find("Raid Chain")?.GetComponent<RaidChain>();
            Check(chain != null && chain.GetComponent<Unity.Netcode.NetworkObject>() != null, "scene chain");
            Check(chain.GetComponent<LineRenderer>().sharedMaterial != null, "chain material");
            Debug.Log("CHAIN_VALIDATION: 100 boundary and state iterations passed; scene=" + scene.name);
        }

        static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Chain validation failed: " + label);
        }
    }
}
