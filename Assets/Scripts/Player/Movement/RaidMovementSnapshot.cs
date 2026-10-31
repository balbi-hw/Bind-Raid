using System;
using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public struct RaidMovementSnapshot : INetworkSerializable, IEquatable<RaidMovementSnapshot>
    {
        public bool Initialized;
        public uint Tick;
        public int InputSequence;
        public uint Revision;
        public Vector3 Position;
        public Quaternion Rotation;
        public float VerticalVelocity;
        public float Stamina;
        public RaidAction Action;
        public float ActionRemaining;
        public Vector3 DodgeDirection;
        public Vector3 ForcedDirection;
        public float ForcedRemaining, ForcedSpeed;
        public bool Braced;
        public bool Playing;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Initialized);
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref InputSequence);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref VerticalVelocity);
            serializer.SerializeValue(ref Stamina);
            serializer.SerializeValue(ref Action);
            serializer.SerializeValue(ref ActionRemaining);
            serializer.SerializeValue(ref DodgeDirection);
            serializer.SerializeValue(ref ForcedDirection);
            serializer.SerializeValue(ref ForcedRemaining);
            serializer.SerializeValue(ref ForcedSpeed);
            serializer.SerializeValue(ref Braced);
            serializer.SerializeValue(ref Playing);
        }

        public RaidMotorState Motor => new RaidMotorState {
            VerticalVelocity = VerticalVelocity, Stamina = Stamina, Action = Action,
            ActionRemaining = ActionRemaining, DodgeDirection = DodgeDirection
        };

        public bool Equals(RaidMovementSnapshot other) => Initialized == other.Initialized && Tick == other.Tick && Revision == other.Revision &&
            InputSequence == other.InputSequence && Position.Equals(other.Position) && Rotation.Equals(other.Rotation) &&
            VerticalVelocity.Equals(other.VerticalVelocity) && Stamina.Equals(other.Stamina) &&
            Action == other.Action && ActionRemaining.Equals(other.ActionRemaining) &&
            ForcedDirection.Equals(other.ForcedDirection) && ForcedRemaining.Equals(other.ForcedRemaining) && ForcedSpeed.Equals(other.ForcedSpeed) &&
            DodgeDirection.Equals(other.DodgeDirection) && Braced == other.Braced && Playing == other.Playing;
    }
}
