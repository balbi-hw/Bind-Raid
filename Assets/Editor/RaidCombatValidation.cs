using System;
using UnityEditor;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class RaidCombatValidation
    {
        [MenuItem("Chainbound Raid/Validate Combat Rules")]
        public static void Validate()
        {
            var attacker = new GameObject("ValidationAttacker");
            var target = new GameObject("ValidationTarget");
            var wall = new GameObject("ValidationWall");
            var weapon = ScriptableObject.CreateInstance<RaidWeaponSettings>();
            try
            {
                var definition = new RaidAttackDefinition { windup = .25f, active = .125f, recovery = .25f, bufferOpen = .5f };
                Require(!definition.IsActive(.249) && definition.IsActive(.25) && definition.IsActive(.374) &&
                    !definition.IsActive(.375), "Damage window boundaries are incorrect.");
                Require(!definition.CanBuffer(.499) && definition.CanBuffer(.5) && !definition.CanBuffer(.625),
                    "Input buffer boundaries are incorrect.");
                var receiver = target.AddComponent<CombatValidationReceiver>();
                target.transform.position = new Vector3(0f, 0f, 2f);
                var layout = ScriptableObject.CreateInstance<RaidHitboxLayout>();
                layout.volumes = new[] {
                    new RaidHitboxLayout.Volume { label = "body", part = 0, size = Vector3.one * .6f },
                    new RaidHitboxLayout.Volume { label = "part", part = 1, size = Vector3.one * .5f }
                };
                layout.Build(target.transform, receiver);
                UnityEngine.Object.DestroyImmediate(layout);
                Physics.SyncTransforms();
                var resolver = new RaidAttackHitResolver();
                var request = new RaidHitRequest { BasePower = 30f };
                resolver.Resolve(attacker.transform, definition, weapon, request, 1, null);
                resolver.Resolve(attacker.transform, definition, weapon, request, 0, null);
                Require(receiver.Hits == 1 && receiver.Part == 1, "Same swing hit a receiver twice or ignored a reachable preferred part.");
                resolver.Reset();
                attacker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                resolver.Resolve(attacker.transform, definition, weapon, request, 1, null);
                Require(receiver.Hits == 1, "Attack hit behind the actor.");
                attacker.transform.rotation = Quaternion.identity;
                target.transform.GetChild(1).localPosition = new Vector3(0f, 0f, 5f);
                Physics.SyncTransforms();
                resolver.Reset();
                resolver.Resolve(attacker.transform, definition, weapon, request, 1, null);
                Require(receiver.Hits == 2 && receiver.Part == 0, "Unreachable selected part received damage.");
                wall.transform.position = new Vector3(0f, 0f, .9f);
                wall.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, .1f);
                Physics.SyncTransforms();
                resolver.Reset();
                resolver.Resolve(attacker.transform, definition, weapon, request, 0, null);
                Require(receiver.Hits == 2, "Attack passed through a wall.");
                wall.transform.position = new Vector3(0f, 0f, .25f);
                Physics.SyncTransforms();
                resolver.Reset();
                resolver.Resolve(attacker.transform, definition, weapon, request, 0, null);
                Require(receiver.Hits == 2, "Attack skipped a wall between the actor and capsule start.");
                wall.SetActive(false);
                request.BasePower = 57f;
                resolver.Reset();
                resolver.Resolve(attacker.transform, definition, weapon, request, 0, null);
                Require(receiver.Hits == 3 && receiver.Power == 57f, "Replacement attack data did not reach the receiver.");
                Debug.Log("COMBAT_VALIDATION: timing, reachable parts, deduplication, facing, obstruction and replacement data passed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(attacker);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(weapon);
            }
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
