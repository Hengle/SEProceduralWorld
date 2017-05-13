﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using BuffPanel.Logging;
using ProcBuild.Utils;
using ProtoBuf;
using Sandbox.Definitions;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.Serialization;

// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace ProcBuild.Library
{
    [Serializable, ProtoContract]
    public class MyObjectBuilder_Part
    {
        [ProtoMember]
        [XmlArrayItem("MyReservedSpace")]
        public MyObjectBuilder_ReservedSpace[] ReservedSpaces;

        [ProtoMember]
        [XmlArrayItem("MountPoint")]
        public MyObjectBuilder_PartMount[] MountPoints;

        [ProtoMember]
        public SerializableVector3I[] OccupiedLocations;

        [ProtoMember]
        [XmlArrayItem("Count")]
        public MySerializableTuple<SerializableDefinitionId, int>[] ComponentCost;

        [ProtoMember]
        [XmlArrayItem("Count")]
        public MySerializableTuple<SerializableDefinitionId, int>[] BlockCountByType;

        [ProtoMember]
        [XmlArrayItem("PowerGroup")]
        public MySerializableTuple<string, float>[] PowerConsumptionByGroup;

        public long ComputeHash()
        {
            var hash = 0L;
            foreach (var a in ReservedSpaces)
                hash ^= a.ComputeHash() * 233L;
            foreach (var a in MountPoints)
                hash ^= a.ComputeHash() * 307L;
            if (OccupiedLocations != null)
                foreach (var a in OccupiedLocations)
                    hash ^= a.GetHashCode() * 2473L;
            if (ComponentCost != null)
                foreach (var kv in ComponentCost)
                    hash ^= kv.Item1.GetHashCode() * 2099L * kv.Item2.GetHashCode();
            if (BlockCountByType != null)
                foreach (var kv in BlockCountByType)
                    hash ^= kv.Item1.GetHashCode() * 2099L * kv.Item2.GetHashCode();
            if (PowerConsumptionByGroup != null)
                foreach (var kv in PowerConsumptionByGroup)
                    hash ^= kv.Item1.GetHashCode() * 65651L * kv.Item2.GetHashCode();
            return hash;
        }
    }

    [Serializable, ProtoContract]
    public class MyObjectBuilder_PartMount
    {
        [ProtoMember]
        public string Name;
        [ProtoMember]
        public string Type;
        [ProtoMember]
        [XmlArrayItem("Block")]
        public MyObjectBuilder_PartMountPointBlock[] Blocks;
        [ProtoMember, DefaultValue(MyAdjacencyRule.Any)]
        public MyAdjacencyRule AdjacencyRule = MyAdjacencyRule.Any;

        public long ComputeHash()
        {
            var hash = 0L;
            if (Name != null)
                hash ^= Name.GetHashCode() * 12007L;
            if (Type != null)
                hash ^= Type.GetHashCode() * 23071L;
            if (Blocks != null)
                foreach (var block in Blocks)
                    hash ^= 563 * block.ComputeHash();
            hash ^= (long)AdjacencyRule * 93169L;
            return hash;
        }
    }

    [Serializable, ProtoContract]
    public class MyObjectBuilder_PartMountPointBlock
    {
        [ProtoMember]
        public string Piece;
        [ProtoMember]
        public Base6Directions.Direction MountDirection6;
        [ProtoMember]
        public SerializableVector3I AnchorLocation;

        public long ComputeHash()
        {
            var hash = 0L;
            if (Piece != null)
                hash ^= Piece.GetHashCode() * 102107L;
            hash ^= (long)MountDirection6 * 85247L;
            hash ^= (long)AnchorLocation.GetHashCode() * 7481L;
            return hash;
        }
    }


    [Serializable, ProtoContract]
    public class MyObjectBuilder_ReservedSpace
    {
        [ProtoMember]
        public SerializableVector3 Min;
        [ProtoMember]
        public SerializableVector3 Max;
        [ProtoMember]
        public bool IsShared;
        [ProtoMember]
        public bool IsOptional;
        
        public long ComputeHash()
        {
            return (Min.GetHashCode() * 67L) ^ (Max.GetHashCode() * 7481L) ^ (IsShared.GetHashCode() * 7) ^ (IsOptional.GetHashCode() * 5);
        }
    }
}