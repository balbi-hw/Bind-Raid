using UnityEngine;

namespace ChainboundRaid
{
    // Render one simulation step behind; never move the authoritative controller.
    public struct RaidPresentationMotion
    {
        Vector3 from, to;
        float sampleTime, stepSeconds;
        bool initialized;
        Quaternion rotationFrom, rotationTo;
        bool rotationInitialized;
        public float Speed { get; private set; }

        public void Record(Vector3 before, Vector3 after, float seconds, float fixedTime)
        {
            from = before; to = after; sampleTime = fixedTime;
            stepSeconds = Mathf.Max(.0001f, seconds); initialized = true;
            Vector3 delta = after - before; delta.y = 0f;
            Speed = delta.magnitude / stepSeconds;
        }

        public Vector3 Offset(Vector3 simulationPosition, float renderTime)
        {
            if (!initialized) return Vector3.zero;
            float t = Mathf.Clamp01((renderTime - sampleTime) / stepSeconds);
            return Vector3.Lerp(from, to, t) - simulationPosition;
        }

        public void RecordRotation(Quaternion before, Quaternion after)
        {
            rotationFrom = before; rotationTo = after; rotationInitialized = true;
        }

        public Quaternion Rotation(Quaternion simulationRotation, float renderTime)
        {
            if (!initialized || !rotationInitialized) return simulationRotation;
            float t = Mathf.Clamp01((renderTime - sampleTime) / stepSeconds);
            return Quaternion.Slerp(rotationFrom, rotationTo, t);
        }

        public void RebaseRotation(Quaternion simulationRotation)
        {
            if (!rotationInitialized) return;
            Quaternion delta = simulationRotation * Quaternion.Inverse(rotationTo);
            rotationFrom = delta * rotationFrom; rotationTo = simulationRotation;
        }

        public void Reset() { initialized = false; rotationInitialized = false; Speed = 0f; }
        public void Rebase(Vector3 simulationPosition)
        {
            if (!initialized) return;
            Vector3 delta = simulationPosition - to;
            from += delta; to = simulationPosition;
        }
    }
}
