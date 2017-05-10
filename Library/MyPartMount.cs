using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRageMath;

namespace ProcBuild
{
    public class MyPartMount
    {
        public readonly string m_mountType;
        public readonly string m_mountName;
        public readonly SortedDictionary<string, List<MyPartMountPointBlock>> m_blocks;
        private readonly MyPart m_part;

        public MyAdjacencyRule AdjacencyRule { private set; get; }

        public IEnumerable<MyPartMountPointBlock> Blocks => m_blocks.Values.SelectMany(x => x);

        public MyPartMount(MyPart part, string mountType, string mountName)
        {
            m_part = part;
            m_mountType = mountType;
            m_mountName = mountName;
            AdjacencyRule = MyAdjacencyRule.Any;
            m_blocks = new SortedDictionary<string, List<MyPartMountPointBlock>>();
        }

        internal void Add(MyPartMountPointBlock block)
        {
            List<MyPartMountPointBlock> points;
            if (!m_blocks.TryGetValue(block.m_piece, out points))
                points = m_blocks[block.m_piece] = new List<MyPartMountPointBlock>(2);
            points.Add(block);
            if (block.AdjacencyRule > AdjacencyRule)
                AdjacencyRule = block.AdjacencyRule;
        }

        private static IEnumerable<MatrixI> GetMultiMatches(IReadOnlyList<MyPartMountPointBlock> mine, IReadOnlyList<MyPartMountPointBlock> other)
        {
            var cache = new HashSet<MatrixI>();
            var match = Math.Min(mine.Count, other.Count);
            if (match == mine.Count)
            {
                foreach (var ot in other)
                    mine[0].GetTransforms(ot, cache);
                cache.RemoveWhere(x =>
                {
                    MatrixI inv;
                    MatrixI.Invert(ref x, out inv);
                    for (var i = 1; i < mine.Count; i++)
                    {
                        var invLoc = Vector3I.Transform(mine[i].MountLocation, ref inv);
                        if (!other.Select(y => y.AnchorLocation).Contains(invLoc)) return true;
                    }
                    return false;
                });
                return cache;
            }
            else
            {
                foreach (var mi in mine)
                    mi.GetTransforms(other[0], cache);
                cache.RemoveWhere(x =>
                {
                    for (var i = 1; i < other.Count; i++)
                    {
                        var loc = Vector3I.Transform(other[i].MountLocation, ref x);
                        if (!mine.Select(y => y.AnchorLocation).Contains(loc)) return true;
                    }
                    return false;
                });
                return cache;
            }
        }

        // Computing mount point transforms is pretty expensive, so we want a fairly large cache.
        // ~256 bytes per entry.  Target a 32MB cache
        private static readonly MyCache<MyTuple<MyPartMount, MyPartMount>, HashSet<MatrixI>> MountCache =
            new MyCache<MyTuple<MyPartMount, MyPartMount>, HashSet<MatrixI>>(32 * 1024 * 1024 / 256);

        private static HashSet<MatrixI> GetTransformInternal(MyTuple<MyPartMount, MyPartMount> meOther)
        {
            var me = meOther.Item1;
            var other = meOther.Item2;
            if (me.m_blocks.Count == 0 || other.m_blocks.Count == 0) return null;
            var adjacencyRule = me.AdjacencyRule > other.AdjacencyRule ? me.AdjacencyRule : other.AdjacencyRule;
            if (adjacencyRule == MyAdjacencyRule.ExcludeSelfPrefab && me.m_part == other.m_part) return null;
            if (adjacencyRule == MyAdjacencyRule.ExcludeSelfMount && me == other) return null;

            // get transforms where all pieces line up.
            // every A must match to an A, etc.
            var options = new HashSet<MatrixI>();
            var availableKeys = me.m_blocks.Keys.Union(other.m_blocks.Keys);
            var init = false;
            foreach (var key in availableKeys)
            {
                var possible = new HashSet<MatrixI>(GetMultiMatches(me.m_blocks[key], other.m_blocks[key]));
                if (!init)
                    options = possible;
                else
                    options.RemoveWhere(x => !possible.Contains(x));
                init = true;
            }
            return options.Count > 0 ? options : null;
        }

        public IEnumerable<MatrixI> GetTransform(MyPartMount otherMount)
        {
            HashSet<MatrixI> output;
            if (MountCache.TryGet(MyTuple.Create(otherMount, this), out output))
                return output?.Select(x =>
                {
                    MatrixI val;
                    MatrixI.Invert(ref x, out val);
                    return val;
                });
            return MountCache.GetOrCreate(MyTuple.Create(this, otherMount), GetTransformInternal);
        }

        // In order to close off this mount point we need at least this region to be free (in part block space)
        private MyTuple<MyPart, MatrixI>? m_smallestTerminalAttachment = null;
        /// <summary>
        /// Gives a best guess on the smallest possible attachment configuration.
        /// </summary>
        public MyTuple<MyPart, MatrixI> SmallestTerminalAttachment
        {
            get
            {
                if (!m_smallestTerminalAttachment.HasValue)
                    m_smallestTerminalAttachment = ComputeSmallestTerminalAttachment();
                return m_smallestTerminalAttachment.Value;
            }
        }

        internal void InvalidateSmallestAttachment()
        {
            m_smallestTerminalAttachment = null;
        }

        private MyTuple<MyPart, MatrixI> ComputeSmallestTerminalAttachment()
        {
            foreach (var part in SessionCore.Instance.PartManager.SortedBySize)
                if (part.MountPointsOfType(m_mountType).Count() <= 2)
                    foreach (var mount in part.MountPointsOfType(m_mountType))
                    {
                        var transforms = GetTransform(mount);
                        if (transforms == null) continue;
                        foreach (var transform in transforms)
                            return MyTuple.Create(part, transform);
                    }
            foreach (var part in SessionCore.Instance.PartManager.SortedBySize)
                foreach (var mount in part.MountPointsOfType(m_mountType))
                {
                    var transforms = GetTransform(mount);
                    if (transforms == null) continue;
                    foreach (var transform in transforms)
                    {
                        SessionCore.Log("Failed to find any terminal module that is attachable to \"{1} {2}\" on {0}.  Resorting to {3}.", m_part.Name, m_mountType, m_mountName, part.Name);
                        return MyTuple.Create(part, transform);
                    }
                }
            SessionCore.Log("Failed to find any module that is attachable to \"{1} {2}\" on {0}", m_part.Name, m_mountType, m_mountName);
            return MyTuple.Create((MyPart)null, default(MatrixI));
        }

    }
}