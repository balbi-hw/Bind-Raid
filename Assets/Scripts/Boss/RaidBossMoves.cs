using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChainboundRaid
{
    public sealed partial class RaidBoss
    {
        readonly Dictionary<BossAttack, (Action begin, Action tick)> moves = new Dictionary<BossAttack, (Action, Action)>();
        void InitializeMoves()
        {
            moves[BossAttack.Stomp] = (BeginStomp, null);
            moves[BossAttack.TrunkSweep] = (BeginTrunkSweep, null);
            moves[BossAttack.Charge] = (BeginCharge, TickCharge);
            moves[BossAttack.ChainSnatch] = (BeginChainSnatch, TickChainSnatch);
            moves[BossAttack.SpinGrab] = (BeginSpinGrab, TickSpinGrab);
        }
        void BeginStomp()
        {

            StompSequence.Value++;
            foreach (var player in RaidSession.Instance.Players)
                if (player != null && FlatDistance(player.transform.position, AttackOrigin.Value) <= settings.stompRadius)
                    HitOnce(player, settings.stompPower, true);
            Recover();
        }

        void BeginTrunkSweep()
        {

            foreach (var player in RaidSession.Instance.Players)
            {
                if (player == null || FlatDistance(player.transform.position, AttackOrigin.Value) > settings.sweepRadius) continue;
                Vector3 relative = player.transform.position - AttackOrigin.Value; relative.y = 0f;
                if (Vector3.Angle(AttackDirection.Value, relative) > settings.sweepHalfAngle) continue;
                HitOnce(player, settings.sweepPower, false);
                player.StartForcedMove(relative, settings.sweepPushDistance, settings.forcedMoveSpeedLimit);
            }
            Recover();
        }

        void BeginCharge()
        {

            SetAttackDuration(settings.chargeDistance / Mathf.Max(.1f, settings.chargeSpeed));
        }

        void BeginChainSnatch()
        {

            if (RaidChain.Instance == null || RaidChain.Instance.PathLength.Value < RaidChain.Instance.warningLength ||
                !RaidChain.Instance.BeginSnatch())
            { SnatchAvoidCount++; Debug.Log("BOSS_EVENT: SnatchAvoided"); Recover(); }
            else SetAttackDuration(settings.snatchResponse);
        }

        void BeginSpinGrab()
        {

            grabbedPlayer = TargetPlayer();
            if (grabbedPlayer == null || FlatDistance(grabbedPlayer.transform.position, transform.position) > settings.spinGrabRange ||
                !grabbedPlayer.SetGrabbedByBoss())
            { Debug.Log("BOSS_EVENT: SpinAvoided"); grabbedPlayer = null; Recover(); }
            else
            {
                SetAttackDuration(settings.spinDuration);
                spinRescueHeld = 0f;
                SpinSpeedFactor.Value = 1f;
            }
        }

        void TickCharge()
        {

            bool hitWall = MoveBoss(AttackDirection.Value * settings.chargeSpeed * Time.deltaTime);
            Vector3 chargeBack = transform.position - transform.forward * settings.chargeBodyBack;
            Vector3 chargeFront = transform.position + transform.forward * settings.chargeBodyFront;
            foreach (var player in RaidSession.Instance.Players)
                if (player != null && FlatDistanceToSegment(player.transform.position,
                    chargeBack, chargeFront) <= settings.chargeWidth)
                    HitOnce(player, settings.chargePower, false);
            if (hitWall)
            { ApplyCooperationStagger(settings.staggerSeconds); return; }
            if (Time.time >= stageEndTime) Recover();
        }

        void TickChainSnatch()
        {

            if (RaidChain.Instance == null || RaidChain.Instance.ActiveAnchor.Value != RaidChainAnchor.Trunk)
            { Recover(); return; }
            if (Time.time < stageEndTime) return;
            if (!snatchResolving)
            {
                snatchResolving = true;
                SnatchPulling.Value = true;
                SetAttackDuration(settings.snatchPullDistance /
                    Mathf.Max(.1f, settings.forcedMoveSpeedLimit));
                foreach (var player in RaidSession.Instance.Players)
                    if (player != null) player.StartForcedMove(transform.position - player.transform.position,
                        settings.snatchPullDistance, settings.forcedMoveSpeedLimit);
                return;
            }
            foreach (var player in RaidSession.Instance.Players)
                if (player != null) HitOnce(player, settings.snatchPower, false);
            RaidChain.Instance.EndSnatch();
            Recover();
        }

        void TickSpinGrab()
        {

            if (grabbedPlayer == null || grabbedPlayer.Action.Value != RaidAction.Grabbed)
            { Recover(); return; }
            TickSpinRescue();
            if (Stage.Value != BossAttackStage.Active || Time.time < stageEndTime) return;
            RaidPlayer free = FreePlayer();
            if (free != null && RaidChain.Instance != null &&
                RaidChain.Instance.PathLength.Value >= RaidChain.Instance.warningLength)
                HitOnce(free, settings.spinTensionPower, false);
            bool slowed = grabbedPlayer.StruggleHeld.Value;
            grabbedPlayer.ReleaseBossGrab();
            HitOnce(grabbedPlayer, settings.spinLandingPower * (slowed ? .75f : 1f), false);
            grabbedPlayer = null;
            Recover();
        }
    }
}
