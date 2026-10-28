using UnityEngine;

namespace ChainboundRaid
{
    public class PrototypeCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 4.5f;
        public float mouseSensitivity = 3f;
        public float collisionRadius = 0.2f;
        float yaw;
        float pitch = 18f;
        int lastShakeSequence;
        float shakeUntil;

        void Update()
        {
            if (RaidSession.Instance == null) return;
            bool combat = RaidSession.Instance.CurrentState == RaidSessionState.Combat;
            if (!combat) Cursor.lockState = CursorLockMode.None;
            else if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            else if (Input.GetMouseButtonDown(0)) Cursor.lockState = CursorLockMode.Locked;
            if (combat && Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -25f, 65f);
            }
            if (combat && target != null && RaidBoss.Instance != null)
            {
                var player = target.GetComponent<RaidPlayer>();
                if (player != null && player.LockEnabled.Value)
                {
                    Vector3 toBoss = RaidBoss.Instance.transform.position - target.position;
                    yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(toBoss.x, toBoss.z) * Mathf.Rad2Deg,
                        4f * Time.deltaTime);
                }
            }
        }

        void LateUpdate()
        {
            if (target == null)
            {
                transform.position = new Vector3(0f, 8f, -12f);
                transform.LookAt(new Vector3(0f, 1f, 2f));
                return;
            }
            Vector3 focus = target.position + Vector3.up * 1.5f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float actualDistance = distance;
            if (Physics.SphereCast(focus, collisionRadius, back, out var hit, distance,
                ~0, QueryTriggerInteraction.Ignore) && hit.transform != target)
                actualDistance = Mathf.Max(0.5f, hit.distance - 0.1f);
            transform.position = Vector3.Lerp(transform.position, focus + back * actualDistance, 12f * Time.deltaTime);
            transform.LookAt(focus);
            var boss = RaidBoss.Instance;
            if (boss != null && boss.Stage.Value == BossAttackStage.Active &&
                boss.AttackSequence.Value != lastShakeSequence)
            {
                lastShakeSequence = boss.AttackSequence.Value;
                shakeUntil = Time.time + .22f;
            }
            if (RaidHud.ScreenShakeEnabled && Time.time < shakeUntil)
                transform.position += new Vector3(
                    Mathf.PerlinNoise(Time.time * 37f, 0f) - .5f,
                    Mathf.PerlinNoise(0f, Time.time * 41f) - .5f, 0f) * .12f;
        }
    }
}
