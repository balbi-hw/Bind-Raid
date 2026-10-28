using UnityEngine;

namespace ChainboundRaid
{
    // Visual-only motion. The root CharacterController and server position are never changed here.
    [RequireComponent(typeof(RaidPlayer))]
    public sealed class RaidPlayerVisual : MonoBehaviour
    {
        public Transform modelRoot;
        public Transform leftArm;
        public Transform rightArm;
        public Transform weapon;
        public Renderer[] coloredParts;
        public LineRenderer statusRing;

        RaidPlayer player;
        MaterialPropertyBlock colorBlock;
        Vector3 previousPosition;
        RaidAction previousAction;
        float actionAge;
        float displayedSpeed;
        bool wasChangingStance;
        float stanceChangeAge;

        void Awake()
        {
            player = GetComponent<RaidPlayer>();
            colorBlock = new MaterialPropertyBlock();
            previousPosition = transform.position;
            if (statusRing != null)
            {
                statusRing.useWorldSpace = false;
                statusRing.loop = true;
                statusRing.positionCount = 32;
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI * 2f / 32f;
                    statusRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.75f, 0.06f,
                        Mathf.Sin(angle) * 0.75f));
                }
            }
        }

        void LateUpdate()
        {
            if (modelRoot == null || player == null || !player.IsSpawned) return;
            RaidAction action = player.Action.Value;
            bool changingStance = player.StanceChanging.Value;
            if (changingStance && !wasChangingStance) stanceChangeAge = 0f;
            if (changingStance) stanceChangeAge += Time.deltaTime;
            wasChangingStance = changingStance;
            if (action != previousAction) { actionAge = 0f; previousAction = action; }
            else actionAge += Time.deltaTime;

            float speed = (transform.position - previousPosition).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            previousPosition = transform.position;
            displayedSpeed = Mathf.Lerp(displayedSpeed, Mathf.Min(speed, 8f), 10f * Time.deltaTime);
            float moveWeight = action == RaidAction.Free ? Mathf.Clamp01(displayedSpeed / 4f) : 0f;
            float walkCycle = Time.time * (6f + displayedSpeed);
            Vector3 position = Vector3.up * (0.025f * Mathf.Sin(Time.time * 2f) +
                0.045f * moveWeight * Mathf.Abs(Mathf.Sin(walkCycle)));
            Vector3 angles = Vector3.zero;
            float armSwing = 24f * moveWeight * Mathf.Sin(walkCycle);
            float weaponSwing = 0f;
            if (action == RaidAction.Free && player.Stance.Value == ChainStance.Braced)
            {
                angles.x = player.PullHeld.Value ? -18f : -8f;
                position.y -= .08f;
                armSwing = 0f;
            }
            if (action == RaidAction.Free && changingStance)
            {
                angles.x = -18f * Mathf.Sin(Mathf.Clamp01(stanceChangeAge / .4f) * Mathf.PI);
                position.y -= .06f;
                armSwing = 0f;
            }

            switch (action)
            {
                case RaidAction.Attack:
                    angles.x = -15f * Mathf.Sin(Mathf.Clamp01(actionAge / 0.55f) * Mathf.PI);
                    weaponSwing = -95f * Mathf.Sin(Mathf.Clamp01(actionAge / 0.55f) * Mathf.PI);
                    break;
                case RaidAction.StrongAttack:
                    angles.x = -28f * Mathf.Sin(Mathf.Clamp01(actionAge / 0.9f) * Mathf.PI);
                    weaponSwing = -155f * Mathf.Sin(Mathf.Clamp01(actionAge / 0.9f) * Mathf.PI);
                    break;
                case RaidAction.Dodge:
                    angles.x = 65f * Mathf.Sin(Mathf.Clamp01(actionAge / 0.3f) * Mathf.PI);
                    position.y -= 0.25f;
                    break;
                case RaidAction.HitReaction:
                    angles.x = 20f * Mathf.Sin(Mathf.Clamp01(actionAge / 0.25f) * Mathf.PI);
                    break;
                case RaidAction.Knockdown:
                    angles.z = 75f;
                    position.y -= 0.45f;
                    break;
                case RaidAction.Grabbed:
                    angles.z = 30f;
                    position.y += 0.15f;
                    break;
                case RaidAction.Incapacitated:
                    angles.z = 90f;
                    position.y -= 0.55f;
                    break;
            }

            modelRoot.localPosition = Vector3.Lerp(modelRoot.localPosition, position, 14f * Time.deltaTime);
            modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation,
                Quaternion.Euler(angles), 14f * Time.deltaTime);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(armSwing, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-armSwing, 0f, 0f);
            if (weapon != null) weapon.localRotation = Quaternion.Euler(0f, 0f, weaponSwing);

            Color baseColor = player.IsOwner ? new Color(0.12f, 0.65f, 1f) : new Color(1f, 0.58f, 0.16f);
            Color color = action switch
            {
                RaidAction.Attack => new Color(1f, 0.9f, 0.25f),
                RaidAction.StrongAttack => new Color(1f, 0.46f, 0.08f),
                RaidAction.Dodge => new Color(0.28f, 1f, 0.8f),
                RaidAction.HitReaction => Color.red,
                RaidAction.Knockdown => new Color(0.95f, 0.2f, 0.35f),
                RaidAction.Grabbed => new Color(0.85f, 0.2f, 1f),
                RaidAction.Incapacitated => new Color(0.55f, 0.1f, 0.1f),
                _ => player.PullHeld.Value ? new Color(.45f, 1f, .4f) :
                    player.Stance.Value == ChainStance.Braced ? new Color(.35f, .8f, 1f) : baseColor
            };
            colorBlock.SetColor("_BaseColor", color);
            foreach (var part in coloredParts)
                if (part != null) part.SetPropertyBlock(colorBlock);
            if (statusRing != null)
            {
                statusRing.startColor = statusRing.endColor = color;
                float pulse = action == RaidAction.Incapacitated ? 1f + 0.1f * Mathf.Sin(Time.time * 7f) : 1f;
                statusRing.transform.localScale = Vector3.one * pulse;
            }
        }
    }
}
