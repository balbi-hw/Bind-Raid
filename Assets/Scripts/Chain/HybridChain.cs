using UnityEngine;

namespace ChainboundRaid
{
    public enum ChainState { Slack, Taut, Overstretched, Wrapped, Snatched, Grabbed, Down }

    public class HybridChain : MonoBehaviour
    {
        public PrototypePlayer first, second;
        public Transform wrapAnchor;
        public LineRenderer visual;
        public float warningLength = 7f, nominalLength = 8f, hardLength = 9f;
        public float pullOverlapSeconds = 0.8f;
        public float staggerGauge;
        public ChainState state { get; private set; }
        public float pathLength { get; private set; }
        public bool snatched, grabbed, down;
        float pullTimer;

        void LateUpdate()
        {
            if (first == null || second == null) return;
            Vector3 a = first.transform.position + Vector3.up, b = second.transform.position + Vector3.up;
            pathLength = wrapAnchor == null ? Vector3.Distance(a, b) : Vector3.Distance(a, wrapAnchor.position) + Vector3.Distance(wrapAnchor.position, b);
            state = down ? ChainState.Down : grabbed ? ChainState.Grabbed : snatched ? ChainState.Snatched :
                wrapAnchor != null ? ChainState.Wrapped : pathLength > hardLength ? ChainState.Overstretched :
                pathLength >= warningLength ? ChainState.Taut : ChainState.Slack;
            // Gameplay uses this simple path. The rendered curve never changes collision or damage.
            if (visual != null)
            {
                visual.positionCount = wrapAnchor == null ? 2 : 3;
                visual.SetPosition(0, a);
                if (wrapAnchor != null) visual.SetPosition(1, wrapAnchor.position);
                visual.SetPosition(visual.positionCount - 1, b);
                visual.startColor = visual.endColor = state == ChainState.Slack ? Color.gray : Color.red;
            }
            if (pathLength > nominalLength && !grabbed && !snatched)
            {
                // Prototype constraint is symmetric and intentionally avoids Rigidbody rope chaos.
                Vector3 delta = b - a; delta.y = 0;
                float excess = Mathf.Min(pathLength - nominalLength, 1f);
                if (delta.sqrMagnitude > 0.01f)
                {
                    first.GetComponent<CharacterController>().Move(delta.normalized * excess * Time.deltaTime * 3f);
                    second.GetComponent<CharacterController>().Move(-delta.normalized * excess * Time.deltaTime * 3f);
                }
            }
            bool bothPull = first.pulling && second.pulling && (state == ChainState.Wrapped || state == ChainState.Snatched);
            pullTimer = bothPull ? pullTimer + Time.deltaTime : 0;
            if (pullTimer >= pullOverlapSeconds) { staggerGauge += Time.deltaTime; }
        }
    }
}
