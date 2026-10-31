using System;
using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Hitbox Layout")]
    public sealed class RaidHitboxLayout : ScriptableObject
    {
        [Serializable]
        public struct Volume
        {
            public string label;
            public int part;
            public Vector3 center;
            public Vector3 size;
            public Vector3 rotation;
        }
        public Volume[] volumes = Array.Empty<Volume>();

        public void Build(Transform root, MonoBehaviour receiver)
        {
            foreach (var volume in volumes)
            {
                var child = new GameObject(volume.label);
                child.layer = root.gameObject.layer;
                child.transform.SetParent(root, false);
                child.transform.localPosition = volume.center;
                child.transform.localRotation = Quaternion.Euler(volume.rotation);
                var collider = child.AddComponent<BoxCollider>();
                collider.size = volume.size;
                collider.isTrigger = true;
                var hurtbox = child.AddComponent<RaidHurtbox>();
                hurtbox.receiver = receiver;
                hurtbox.part = volume.part;
            }
        }
    }
}
