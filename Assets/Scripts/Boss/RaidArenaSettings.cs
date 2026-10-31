using UnityEngine;
namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Arena Bounds")]
    public sealed class RaidArenaSettings : ScriptableObject
    {
        public Vector3 center;
        public Vector2 halfExtents;
        public Vector3 Clamp(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, center.x - halfExtents.x, center.x + halfExtents.x);
            position.z = Mathf.Clamp(position.z, center.z - halfExtents.y, center.z + halfExtents.y);
            return position;
        }
    }
}
