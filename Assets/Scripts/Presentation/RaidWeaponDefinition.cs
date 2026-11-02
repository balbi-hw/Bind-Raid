using UnityEngine;

namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Weapon Visual")]
    public sealed class RaidWeaponDefinition : ScriptableObject
    {
        public string weaponId;
        public GameObject modelPrefab;
        public Vector3 gripPosition = new Vector3(0, .07f, .015f);
        public Vector3 gripEuler = new Vector3(0, 0, 90);
        [Range(0, 90)] public float fingerCurlDegrees = 65f;
        public float modelScale = 1f;
        [Header("Locomotion carry pose (character space)")]
        public bool useCarryPose;
        public Vector3 carryDirection = new Vector3(.35f, .8f, .55f);
        public float minimumHandSide = .28f;
        public float minimumHandForward = .12f;
        [Header("Two-handed heavy attack")]
        public bool twoHandedHeavy;
        public Vector3 supportGrip = new Vector3(0, -.085f, 0);
    }
}
