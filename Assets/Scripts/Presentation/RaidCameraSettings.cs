using UnityEngine;
namespace ChainboundRaid
{
    [CreateAssetMenu(menuName = "Chainbound Raid/Camera Settings")]
    public sealed class RaidCameraSettings : ScriptableObject
    {
        public float distance = 6.5f, maximumDistance = 11f, focusHeight = 1.4f;
        public float minimumPitch = -25f, maximumPitch = 65f;
        public float collisionRadius = .2f, minimumDistance = .5f;
        public float collisionPadding = .1f, returnSpeed = 4f;
        public float focusSmoothSeconds = .08f, lockRotationSharpness = 4f;
        public float lockFocusBlend = .35f, maxFocusOffset = 3f;
        public float shakeSeconds = .18f, shakeAmplitude = .12f;
        public Vector3 lobbyPosition, lobbyFocus;
        public LayerMask collisionLayers = ~0;
    }
}
