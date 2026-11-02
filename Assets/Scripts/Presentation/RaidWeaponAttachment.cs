using UnityEngine;

namespace ChainboundRaid
{
    // Visual equipment only. Damage and network-authoritative attacks remain on RaidPlayer.
    public sealed class RaidWeaponAttachment : MonoBehaviour
    {
        public Transform hand;
        public RaidWeaponDefinition equippedWeapon;
        [SerializeField] GameObject equippedModel;
        Transform[] gripBones;
        Transform supportHand;
        Transform[] supportFingers;
        public GameObject EquippedModel => equippedModel;
        public float SupportGripError { get; private set; }

        public void ApplyHeavyGrip(float normalizedTime, bool heavy)
        {
            SupportGripError = 0;
            if (!heavy || equippedWeapon == null || !equippedWeapon.twoHandedHeavy || equippedModel == null) return;
            if (supportHand == null)
            {
                foreach (var joint in transform.GetComponentsInChildren<Transform>(true))
                    if (joint.name == "mixamorig:LeftHand") { supportHand = joint; break; }
                if (supportHand != null) supportFingers = supportHand.GetComponentsInChildren<Transform>();
            }
            if (supportHand == null) return;
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(normalizedTime / (6f/36f))) *
                (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(29f/36f, 1, normalizedTime)));
            if (weight <= 0) return;
            var lower = supportHand.parent; var upper = lower.parent;
            Quaternion desiredRotation=hand.rotation*Quaternion.Euler(0,0,180);
            Vector3 offset=desiredRotation*equippedWeapon.gripPosition*transform.lossyScale.x;
            Vector3 grip=equippedModel.transform.TransformPoint(equippedWeapon.supportGrip);
            Vector3 target=Vector3.Lerp(supportHand.position,grip-offset,weight);
            target += transform.forward * (.16f * 4f * weight * (1f-weight)) * transform.lossyScale.x;
            desiredRotation=Quaternion.Slerp(supportHand.rotation,desiredRotation,weight);
            Vector3 root=upper.position, axis=(target-root).normalized;
            float a=Vector3.Distance(root,lower.position), b=Vector3.Distance(lower.position,supportHand.position);
            float distance=Mathf.Clamp(Vector3.Distance(root,target),Mathf.Abs(a-b)+.0001f,a+b-.0001f);
            Vector3 pole=transform.TransformDirection(new Vector3(-1, -.25f, .6f));
            pole=Vector3.ProjectOnPlane(pole,axis).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            Vector3 elbow=root+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-root,elbow-root)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(supportHand.position-lower.position,root+axis*distance-lower.position)*lower.rotation;
            supportHand.rotation=desiredRotation;
            SupportGripError=Vector3.Distance(supportHand.position+supportHand.rotation*equippedWeapon.gripPosition*transform.lossyScale.x,grip);
            foreach(var finger in supportFingers)
                if(!finger.name.Contains("Thumb") && (finger.name.EndsWith("1")||finger.name.EndsWith("2")||finger.name.EndsWith("3")))
                    finger.localRotation*=Quaternion.AngleAxis(-equippedWeapon.fingerCurlDegrees*weight,Vector3.right);
        }

        // Rotate the animated arm, never translate/stretch bones or change the gait.
        // Run after the animation graph and before finger curl, only during free locomotion.
        public void ApplyCarryPose(float speed, bool locomotion)
        {
            if (!locomotion || equippedWeapon == null || !equippedWeapon.useCarryPose ||
                hand == null || equippedModel == null || hand.parent == null || hand.parent.parent == null) return;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, 3f, speed));
            if (weight <= 0f) return;
            var upperArm = hand.parent.parent;
            Vector3 target = transform.InverseTransformPoint(hand.position);
            target.x = Mathf.Max(target.x, equippedWeapon.minimumHandSide);
            target.z = Mathf.Max(target.z, equippedWeapon.minimumHandForward);
            Vector3 current = hand.position - upperArm.position;
            Vector3 desired = transform.TransformPoint(target) - upperArm.position;
            if (current.sqrMagnitude > .0001f && desired.sqrMagnitude > .0001f)
                upperArm.rotation = Quaternion.Slerp(Quaternion.identity,
                    Quaternion.FromToRotation(current, desired), weight) * upperArm.rotation;
            Vector3 direction = transform.TransformDirection(equippedWeapon.carryDirection);
            if (direction.sqrMagnitude > .0001f)
                hand.rotation = Quaternion.Slerp(Quaternion.identity,
                    Quaternion.FromToRotation(equippedModel.transform.up, direction), weight) * hand.rotation;
        }

        // Called immediately after animation evaluation, so this offset cannot accumulate.
        public void ApplyGripPose()
        {
            if (equippedWeapon == null || hand == null) return;
            if (gripBones == null) gripBones = hand.GetComponentsInChildren<Transform>();
            foreach (var finger in gripBones)
            {
                string n = finger.name;
                if (n.Contains("Thumb") || !n.Contains("RightHand") ||
                    !(n.EndsWith("1") || n.EndsWith("2") || n.EndsWith("3"))) continue;
                finger.localRotation *= Quaternion.AngleAxis(equippedWeapon.fingerCurlDegrees, Vector3.right);
            }
        }

        void Awake()
        {
            if (equippedModel == null && equippedWeapon != null) Equip(equippedWeapon);
        }

        public void Equip(RaidWeaponDefinition definition)
        {
            if (definition != null && (hand == null || definition.modelPrefab == null))
            { Debug.LogError("Weapon requires a hand socket and model prefab.", this); return; }
            if (equippedModel != null)
            {
                equippedModel.SetActive(false);
                if (Application.isPlaying) Destroy(equippedModel); else DestroyImmediate(equippedModel);
            }
            equippedWeapon = definition;
            if (definition == null) return;
            equippedModel = Instantiate(definition.modelPrefab, hand, false);
            equippedModel.name = "Equipped " + definition.weaponId;
            // Imported skeletons may carry a unit scale on the armature. Offsets are in model metres.
            Vector3 scale = hand.lossyScale;
            float factor = transform.lossyScale.x;
            equippedModel.transform.localPosition = new Vector3(
                definition.gripPosition.x / scale.x, definition.gripPosition.y / scale.y,
                definition.gripPosition.z / scale.z) * factor;
            equippedModel.transform.localRotation = Quaternion.Euler(definition.gripEuler);
            equippedModel.transform.localScale = new Vector3(factor / scale.x, factor / scale.y, factor / scale.z) * definition.modelScale;
        }
    }
}
