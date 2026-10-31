using System.Collections.Generic;
using UnityEngine;
namespace ChainboundRaid
{
    public sealed class RaidTargetFocus : MonoBehaviour
    {
        RaidHurtbox[] parts;
        readonly HashSet<int> unavailable = new HashSet<int>();
        public void Refresh() { parts = GetComponentsInChildren<RaidHurtbox>(); }
        public void SetSelectable(int part, bool selectable) { if (selectable) unavailable.Remove(part); else unavailable.Add(part); }
        public void ResetParts() { unavailable.Clear(); }
        public bool IsSelectable(int part) => !unavailable.Contains(part);
        public bool TryGetFocus(int part, out Vector3 point, out float radius)
        {
            point = transform.position; radius = 1f;
            if (parts == null) Refresh();
            bool found = false; Bounds bounds = default;
            foreach (var item in parts)
            {
                if (item == null || !item.isActiveAndEnabled || item.part != part || !IsSelectable(part)) continue;
                var shape = item.GetComponent<Collider>();
                if (!shape.enabled) continue;
                if (!found) bounds = shape.bounds; else bounds.Encapsulate(shape.bounds);
                found = true;
            }
            if (found) { point = bounds.center; radius = bounds.extents.magnitude; }
            return found;
        }
    }
}
