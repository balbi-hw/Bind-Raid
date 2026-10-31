using UnityEngine;

namespace ChainboundRaid
{
    public sealed class RaidHitFlash : MonoBehaviour
    {
        float remaining = .12f;
        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * (.2f * remaining / .12f);
        }
    }
}
