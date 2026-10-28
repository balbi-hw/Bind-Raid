using UnityEngine;

namespace ChainboundRaid
{
    public enum BossMove { Idle, Stomp, Charge, TrunkSweep, ChainSnatch, SpinGrab, Staggered }

    public class ElephantBossPrototype : MonoBehaviour
    {
        public HybridChain chain;
        public PrototypePlayer first, second;
        public float health = 1000f;
        public BossMove currentMove;
        public float telegraphEnd, recoveryEnd;
        public bool attackActive;
        float nextAttack;

        void Update()
        {
            if (health <= 0) return;
            if (chain != null && chain.staggerGauge >= 2f)
            { currentMove = BossMove.Staggered; chain.staggerGauge = 0; chain.snatched = false; recoveryEnd = Time.time + 3f; nextAttack = recoveryEnd; }
            if (Time.time < nextAttack) return;
            if (!attackActive)
            {
                currentMove = (BossMove)Random.Range(1, 6);
                telegraphEnd = Time.time + (currentMove == BossMove.SpinGrab ? 1.8f : currentMove == BossMove.ChainSnatch ? 1.4f : 1.2f);
                attackActive = true;
                return;
            }
            if (Time.time < telegraphEnd) return;
            ResolveMove();
            attackActive = false;
            nextAttack = Time.time + 2.5f;
        }

        void ResolveMove()
        {
            if (chain == null || first == null || second == null) return;
            if (currentMove == BossMove.ChainSnatch && chain.pathLength >= chain.warningLength)
            { chain.snatched = true; Invoke(nameof(ReleaseGrab), 3f); }
            else if (currentMove == BossMove.SpinGrab)
            {
                chain.grabbed = true;
                PrototypePlayer target = Vector3.Distance(first.transform.position, transform.position) < Vector3.Distance(second.transform.position, transform.position) ? first : second;
                PrototypePlayer free = target == first ? second : first;
                if (Vector3.Distance(free.transform.position, transform.position) >= 7f) free.ApplyDamage(25f);
                target.ApplyDamage(15f);
                Invoke(nameof(ReleaseGrab), 2f);
            }
            else if (currentMove == BossMove.Stomp)
            {
                if (Vector3.Distance(first.transform.position, transform.position) < 4f) first.ApplyDamage(20f);
                if (Vector3.Distance(second.transform.position, transform.position) < 4f) second.ApplyDamage(20f);
            }
        }
        void ReleaseGrab() { if (chain != null) { chain.grabbed = false; chain.snatched = false; } }
    }
}
