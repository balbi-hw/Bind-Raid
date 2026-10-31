using UnityEngine;
namespace ChainboundRaid
{
    [DefaultExecutionOrder(-100)]
    public class PrototypeCamera : MonoBehaviour
    {
        public Transform target;
        public RaidCameraSettings settings;
        RaidCameraSettings config;
        bool ownsSettings;
        RaidPlayer followed;
        Transform previousTarget;
        Camera view;
        float yaw, pitch = 18f, actualDistance, shakeUntil;
        Vector3 smoothedFocus, focusVelocity;
        int lastDamageSequence;
        bool initialized;
        void Awake()
        {
            config = settings;
            if (config == null) { config = ScriptableObject.CreateInstance<RaidCameraSettings>(); ownsSettings = true; }
            view = GetComponent<Camera>();
            actualDistance = config.distance;
        }
        void OnDestroy() { if (ownsSettings) Destroy(config); }
        void Update()
        {
            if (previousTarget != target)
            {
                previousTarget = target; followed = target != null ? target.GetComponent<RaidPlayer>() : null;
                initialized = false;
                lastDamageSequence = followed != null ? followed.DamageSequence.Value : 0;
            }
            if (!RaidInputContext.AcceptingInput) return;
            yaw += Input.GetAxis("Mouse X") * RaidUserSettings.Sensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * RaidUserSettings.Sensitivity, config.minimumPitch, config.maximumPitch);
            if (followed != null && followed.TryGetLockFocus(out var point, out _))
            {
                Vector3 direction = point - target.position;
                yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg,
                    1f - Mathf.Exp(-config.lockRotationSharpness * Time.deltaTime));
            }
        }
        void LateUpdate()
        {
            if (target == null)
            {
                transform.position = config.lobbyPosition;
                if ((config.lobbyFocus - config.lobbyPosition).sqrMagnitude > .001f) transform.LookAt(config.lobbyFocus);
                return;
            }
            Vector3 focus = target.position + Vector3.up * config.focusHeight + (followed != null ? followed.PresentationOffset : Vector3.zero);
            float desiredDistance = config.distance;
            if (followed != null && followed.TryGetLockFocus(out var point, out float radius))
            {
                focus += Vector3.ClampMagnitude((point - focus) * config.lockFocusBlend, config.maxFocusOffset);
                float fov = view != null ? view.fieldOfView : 60f;
                desiredDistance = Mathf.Clamp(radius / Mathf.Tan(fov * .5f * Mathf.Deg2Rad), config.distance, config.maximumDistance);
            }
            if (!initialized) { smoothedFocus = focus; initialized = true; }
            smoothedFocus = Vector3.SmoothDamp(smoothedFocus, focus, ref focusVelocity, config.focusSmoothSeconds);
            Vector3 back = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
            float clearDistance = desiredDistance;
            foreach (var hit in Physics.SphereCastAll(smoothedFocus, config.collisionRadius, back, desiredDistance,
                config.collisionLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == target || hit.transform.IsChildOf(target) || hit.collider.GetComponentInParent<RaidPlayer>() != null) continue;
                clearDistance = Mathf.Min(clearDistance, Mathf.Max(config.minimumDistance, hit.distance - config.collisionPadding));
            }
            actualDistance = clearDistance < actualDistance ? clearDistance :
                Mathf.MoveTowards(actualDistance, clearDistance, config.returnSpeed * Time.deltaTime);
            transform.position = smoothedFocus + back * actualDistance;
            transform.LookAt(smoothedFocus);
            if (followed != null && followed.DamageSequence.Value != lastDamageSequence)
            { lastDamageSequence = followed.DamageSequence.Value; shakeUntil = Time.time + config.shakeSeconds; }
            if (RaidUserSettings.ScreenShake && Time.time < shakeUntil)
                transform.position += transform.right * (Mathf.PerlinNoise(Time.time * 37f, 0f) - .5f) * config.shakeAmplitude +
                    transform.up * (Mathf.PerlinNoise(0f, Time.time * 41f) - .5f) * config.shakeAmplitude;
        }
    }
}
