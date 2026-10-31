using System.Collections.Generic;
using UnityEngine;

namespace ChainboundRaid
{
    // One accepted hit per receiver per swing. Selection never makes an unreachable part hittable.
    public sealed class RaidAttackHitResolver
    {
        readonly HashSet<IRaidDamageReceiver> hitReceivers = new HashSet<IRaidDamageReceiver>();
        readonly Dictionary<IRaidDamageReceiver, RaidHurtbox> candidates = new Dictionary<IRaidDamageReceiver, RaidHurtbox>();
        public void Reset() { hitReceivers.Clear(); candidates.Clear(); }

        public void Resolve(Transform attacker, RaidAttackDefinition attack, RaidWeaponSettings weapon,
            RaidHitRequest request, int preferredPart, System.Action<RaidHitRequest> confirmed)
        {
            Vector3 start = attacker.TransformPoint(attack.capsuleStart);
            Vector3 end = attacker.TransformPoint(attack.capsuleEnd);
            Vector3 origin = attacker.position + attacker.up * attack.capsuleStart.y;
            candidates.Clear();
            foreach (var shape in Physics.OverlapCapsule(start, end, attack.radius, weapon.targetLayers, QueryTriggerInteraction.Collide))
            {
                var hurtbox = shape.GetComponent<RaidHurtbox>();
                if (hurtbox == null || !hurtbox.isActiveAndEnabled || hurtbox.receiver == null || hurtbox.Receiver == null || hitReceivers.Contains(hurtbox.Receiver)) continue;
                if (hurtbox.receiver.transform == attacker || hurtbox.receiver.transform.IsChildOf(attacker)) continue;
                Vector3 contact = shape.ClosestPoint(start);
                if (Physics.Linecast(origin, contact, out var obstruction, weapon.obstructionLayers, QueryTriggerInteraction.Ignore) &&
                    !obstruction.collider.transform.IsChildOf(hurtbox.receiver.transform)) continue;
                if (!candidates.TryGetValue(hurtbox.Receiver, out var previous) ||
                    (hurtbox.part == preferredPart && previous.part != preferredPart) ||
                    (hurtbox.part != preferredPart && previous.part != preferredPart &&
                     (contact - start).sqrMagnitude < (previous.GetComponent<Collider>().ClosestPoint(start) - start).sqrMagnitude))
                    candidates[hurtbox.Receiver] = hurtbox;
            }
            foreach (var pair in candidates)
            {
                request.Part = pair.Value.part;
                request.Point = pair.Value.GetComponent<Collider>().ClosestPoint(start);
                if (!pair.Key.TryReceiveHit(request)) continue;
                hitReceivers.Add(pair.Key);
                confirmed?.Invoke(request);
            }
        }
    }
}
