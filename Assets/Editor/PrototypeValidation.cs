using System;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

namespace ChainboundRaid.Editor
{
    public static class PrototypeValidation
    {
        [MenuItem("Chainbound Raid/Validate 01-02")]
        public static void Validate()
        {
            if (ReviveRules.RequiredSeconds(1) != 3f ||
                ReviveRules.RequiredSeconds(2) != 6f ||
                ReviveRules.RequiredSeconds(3) != 9f)
                throw new InvalidOperationException("Per-player revive duration progression is incorrect.");
            float baseDamage = DamageCalculator.Calculate(30f, 40f, 10f);
            if (DamageCalculator.Calculate(30f, 80f, 10f) <= baseDamage ||
                DamageCalculator.Calculate(30f, 40f, 40f) >= baseDamage)
                throw new InvalidOperationException("Attack and defense do not affect damage correctly.");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/RaidSessionScene.unity");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkRaidPlayer.prefab");
            if (scene == null || prefab == null || prefab.GetComponent<NetworkObject>() == null ||
                prefab.GetComponent<RaidPlayer>() == null)
                throw new InvalidOperationException("01-02 network scene or player prefab is missing.");
            Debug.Log("RAID_VALIDATION: 01-02 rules and assets passed");
        }
    }
}
