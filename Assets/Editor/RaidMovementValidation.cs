using System;
using UnityEditor;
using UnityEngine;

namespace ChainboundRaid.Editor
{
    public static class RaidMovementValidation
    {
        [MenuItem("Chainbound Raid/Validate Movement Motor")]
        public static void Validate()
        {
            var settings = ScriptableObject.CreateInstance<RaidMovementSettings>();
            try
            {
                foreach (int rate in new[] { 30, 50, 120 })
                {
                    var state = new RaidMotorState { Stamina = 100f };
                    Vector3 travel = Vector3.zero;
                    for (int i = 0; i < rate; i++)
                        travel += RaidMovementMotor.Step(ref state,
                            new RaidMoveInput { Direction = Vector3.forward * 8f, Sprint = true },
                            settings, true, false, Vector3.forward, 1f / rate);
                    Require(Mathf.Abs(travel.z - settings.sprintSpeed) < .001f, "Movement depends on step rate or input magnitude.");
                    Require(Mathf.Abs(state.Stamina - (100f - settings.sprintCostPerSecond)) < .002f, "Sprint drain depends on step rate.");
                }
                foreach (var action in new[] { RaidAction.Attack, RaidAction.StrongAttack,
                    RaidAction.Dodge, RaidAction.HitReaction, RaidAction.Knockdown })
                {
                    var state = new RaidMotorState { Action = action, ActionRemaining = .2f, Stamina = 100f };
                    for (int i = 0; i < 20; i++)
                        RaidMovementMotor.Step(ref state, default, settings, true, false, Vector3.forward, .02f);
                    Require(state.Action == RaidAction.Free, "Timed action did not end without input: " + action);
                }
                foreach (var action in new[] { RaidAction.Grabbed, RaidAction.Incapacitated })
                {
                    var state = new RaidMotorState { Action = action, Stamina = 100f };
                    var delta = RaidMovementMotor.Step(ref state, new RaidMoveInput {
                        Direction = Vector3.forward, Sprint = true, Dodge = true
                    }, settings, true, false, Vector3.forward, .02f);
                    Require(delta == Vector3.zero && state.Action == action, "Disabled actor accepted movement or dodge.");
                }
                var dodge = new RaidMotorState { Stamina = settings.dodgeCost - 1f };
                RaidMovementMotor.Step(ref dodge, new RaidMoveInput { Dodge = true },
                    settings, true, false, Vector3.forward, .02f);
                Require(dodge.Action != RaidAction.Dodge, "Dodge allowed with insufficient stamina.");
                dodge.Stamina = 100f;
                RaidMovementMotor.Step(ref dodge, new RaidMoveInput { Dodge = true },
                    settings, true, false, Vector3.forward, .02f);
                Require(dodge.Action == RaidAction.Dodge && dodge.DodgeDirection == Vector3.forward,
                    "Stationary dodge did not use actor facing.");
                float afterDodge = dodge.Stamina;
                RaidMovementMotor.Step(ref dodge, new RaidMoveInput { Dodge = true },
                    settings, true, false, Vector3.forward, .02f);
                Require(dodge.Stamina >= afterDodge, "Active dodge charged stamina twice.");
                var braced = new RaidMotorState { Stamina = 100f };
                var bracedMove = RaidMovementMotor.Step(ref braced, new RaidMoveInput { Direction = Vector3.right },
                    settings, true, true, Vector3.forward, .02f);
                Require(Mathf.Abs(bracedMove.x - settings.walkSpeed * settings.bracedSpeedMultiplier * .02f) < .0001f,
                    "Braced movement multiplier changed.");
                Debug.Log("MOVEMENT_VALIDATION: motor checks passed");
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
