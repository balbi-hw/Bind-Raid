using UnityEngine;
namespace ChainboundRaid
{
    [DefaultExecutionOrder(-200)]
    public sealed class RaidInputController : MonoBehaviour
    {
        void Update()
        {
            var session = RaidSession.Instance;
            RaidInputContext.Update(session != null && (session.CurrentState == RaidSessionState.Lobby ||
                session.CurrentState == RaidSessionState.Tutorial ||
                session.CurrentState == RaidSessionState.Combat));
        }
        void OnDisable() => RaidInputContext.Update(false);
    }
}
