using System;
using Unity.Netcode;
using UnityEngine;

namespace ChainboundRaid
{
    public struct RaidAttackPlayback : INetworkSerializable, IEquatable<RaidAttackPlayback>
    {
        public uint Instance;
        public int Definition;
        public double StartedAt;
        public Vector3 Heading;
        public bool Active;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Instance);
            serializer.SerializeValue(ref Definition);
            serializer.SerializeValue(ref StartedAt);
            serializer.SerializeValue(ref Heading);
            serializer.SerializeValue(ref Active);
        }
        public bool Equals(RaidAttackPlayback other) => Instance == other.Instance && Definition == other.Definition &&
            StartedAt.Equals(other.StartedAt) && Heading.Equals(other.Heading) && Active == other.Active;
    }

    public struct RaidHitRequest
    {
        public ulong Attacker;
        public uint AttackInstance;
        public string AttackId;
        public int Part;
        public float BasePower;
        public float AttackPower;
        public float PartPower;
        public Vector3 Point;
    }

    public interface IRaidDamageReceiver
    {
        bool TryReceiveHit(RaidHitRequest hit);
    }
}
