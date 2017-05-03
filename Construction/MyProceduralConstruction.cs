﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRageMath;

namespace ProcBuild.Construction
{
    public class MyProceduralConstruction
    {
        private Dictionary<long, MyProceduralRoom> m_rooms;

        public MyProceduralConstruction()
        {
            m_maxID = 0;
        }

        public void Init(MyObjectBuilder_ProceduralConstruction ob)
        {
            m_rooms.Clear();
            m_maxID = 0;
            foreach (var room in ob.Room)
            {
                new MyProceduralRoom().Init(room, this);
            }
        }

        internal void RegisterRoom(MyProceduralRoom room)
        {
            if (m_rooms.ContainsKey(room.RoomID))
                throw new ArgumentException("Room ID already used");
            m_maxID = Math.Max(m_maxID, room.RoomID);
            m_rooms[room.RoomID] = room;
        }

        private long m_maxID;
        internal long AcquireID()
        {
            m_maxID++;
            return m_maxID;
        }
    }

    public class MyProceduralRoom
    {
        public MyProceduralConstruction Owner { get; private set; }
        public long RoomID { get; private set; }
        public MyPart Prefab { get; private set; }
        private MatrixI m_transform;
        public MyProceduralMountPoint[] MountPoints { get; private set; }

        public MyProceduralRoom()
        {
            Owner = null;
            RoomID = -1;
        }

        public void Init(MyObjectBuilder_ProceduralRoom ob, MyProceduralConstruction parent)
        {
            Owner = parent;
            RoomID = ob.RoomID;
            Prefab = SessionCore.Instance.PartManager.LoadNullable(ob.PrefabID);
            Transform = ob.Transform;
            MountPoints = new MyProceduralMountPoint[ob.MountPoints.Length];
            for (var i = 0; i < MountPoints.Length; i++)
            {
                MountPoints[i] = new MyProceduralMountPoint();
                MountPoints[i].Init(ob.MountPoints[i], this);
            }
            parent.RegisterRoom(this);
        }

        public MatrixI Transform
        {
            get { return m_transform; }
            set
            {
                m_transform = value;
                BoundingBox = MyUtilities.TransformBoundingBox(Prefab.m_boundingBox, value);
            }
        }

        public BoundingBox BoundingBox { get; private set; }
    }

    public class MyProceduralMountPoint
    {
        public MyProceduralRoom Owner { get; private set; }
        public MyPartMount MountPoint { get; private set; }

        public MyProceduralMountPoint()
        {
            Owner = null;
            MountPoint = null;
        }

        public void Init(MyObjectBuilder_ProceduralMountPoint ob, MyProceduralRoom parent)
        {
            Owner = parent;
            MountPoint = parent.Prefab.MountPoint(ob.TypeID, ob.InstanceID);
        }
    }
}
