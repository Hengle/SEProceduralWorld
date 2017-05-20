﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuffPanel.Logging;
using ProcBuild.Library;
using ProcBuild.Storage;
using VRage;
using VRageMath;
using ProcBuild.Utils;
using Sandbox.ModAPI;

namespace ProcBuild.Generation
{
    public partial class MyGenerator
    {
        private static bool ValidateAddition(MyProceduralConstruction c, MyProceduralRoom room, bool testOptional)
        {
            // Buildable?
            if (c.Intersects(room, testOptional))
                return false;

            // Reject if this will block another mount point, or one of our mount points would blocked.
            var selfRooms = new List<MyTuple<MyPart, MatrixI, MatrixI>>();
            foreach (var point in room.MountPoints)
                if (point.AttachedTo == null)
                {
                    var oppos = point.MountPoint.SmallestTerminalAttachment;
                    if (oppos.Item1 == null) continue;
                    var pos = MyUtilities.Multiply(oppos.Item2, room.Transform);
                    MatrixI ipos;
                    MatrixI.Invert(ref pos, out ipos);
                    if (c.Intersects(oppos.Item1, pos, ipos, testOptional))
                        return false;
                    selfRooms.Add(MyTuple.Create(oppos.Item1, pos, ipos));
                }

            // Compare to all other unused mount points.  Cache these?
            foreach (var other in c.Rooms)
                if (other != room)
                    foreach (var point in other.MountPoints)
                        if (point.AttachedTo == null)
                        {
                            var oppos = point.MountPoint.SmallestTerminalAttachment;
                            if (oppos.Item1 == null) continue;
                            var pos = MyUtilities.Multiply(oppos.Item2, other.Transform);
                            MatrixI ipos;
                            MatrixI.Invert(ref pos, out ipos);
                            if (!selfRooms.Any(self => MyPartStorage.Intersects(self.Item1, self.Item2, self.Item3, other.Part, pos, ipos, testOptional))) continue;
                            return false;
                        }
            return true;
        }

        public static bool StepConstruction(MyProceduralConstruction c, float targetGrowth = 0, bool testOptional = true, Func<MyPart, bool> filter = null)
        {
            var freeMountPoints = c.Rooms.SelectMany(x => x.MountPoints).Where(x => x.AttachedTo == null).ToList();

            var iwatch = new Stopwatch();
            iwatch.Restart();
            // Compute Available Rooms
            var availableRooms = new Dictionary<MyTuple<MyPart, MatrixI>, MyProceduralRoom>();
            foreach (var type in SessionCore.Instance.PartManager)
                if (filter == null || filter.Invoke(type))
                    foreach (var point in freeMountPoints)
                        foreach (var other in type.MountPointsOfType(point.MountPoint.MountType))
                        {
                            var mats = point.MountPoint.GetTransform(other);
                            if (mats == null) continue;
                            foreach (var mat in mats)
                            {
                                var actual = MyUtilities.Multiply(mat, point.Owner.Transform);
                                var key = MyTuple.Create(type, actual);
                                if (!availableRooms.ContainsKey(key))
                                    c.RemoveRoom(availableRooms[key] = c.GenerateRoom(actual, type));
                            }
                        }
            if (Settings.Instance.DebugGenerationStages)
                SessionCore.Log("Choose from {0} options; generated in {1}", availableRooms.Count, iwatch.Elapsed);

            iwatch.Restart();
            // Compute room weights
            var weightedRoomChoice = new MyWeightedChoice<MyProceduralRoom>();

            var originalError = new List<string>();
            var originalRequirementError = c.ComputeErrorAgainstSeed(MyUtilities.LogToList(originalError));

            var collisionWatch = new Stopwatch();
            var inoutFactorWatch = new Stopwatch();
            var usefulnessWatch = new Stopwatch();

            var roomErrorCache = new Dictionary<MyPart, double>();

            var bestError = double.MaxValue;
            var bestErrMux = new List<string>();
            MyProceduralRoom bestRoom = null;
            foreach (var room in availableRooms.Values)
            {
                try
                {
                    collisionWatch.Start();
                    room.TakeOwnership(c);
                    if (!ValidateAddition(c, room, testOptional))
                        continue;
                    collisionWatch.Stop();

                    var coolness = 0.0;
                    inoutFactorWatch.Start();
                    if (!float.IsNaN(targetGrowth))
                    { // Based on the target growth and destruct rates
                        var count = 0;
                        foreach (var point in room.MountPoints)
                            if (point.AttachedTo != null)
                                count--;
                            else
                                count++;

                        if (targetGrowth < 0 && count > 0)
                            coolness -= 1e10; // super discouraged.  Last resort type thing.
                        var error = count - targetGrowth;
                        coolness += 100 * (1 - error * error);
                    }
                    inoutFactorWatch.Stop();

                    { // Coolness based on navmesh distance vs real distance.
                      // If we do this we need to include the future rooms.
                    }
                    { // Less coolness for gravity generators with different orientations

                    }

                    usefulnessWatch.Start();
                    { // Coolness based on how useful this room will be.
                      // If we need power and this room provides power this is high, etc...

                        // This is the same per added room type.
                        double newRequirementError;
                        if (!roomErrorCache.TryGetValue(room.Part, out newRequirementError))
                            roomErrorCache[room.Part] = newRequirementError = c.ComputeErrorAgainstSeed();
                        if (bestError > newRequirementError)
                        {
                            bestErrMux.Clear();
                            c.ComputeErrorAgainstSeed(MyUtilities.LogToList(bestErrMux));
                            bestError = newRequirementError;
                            bestRoom = room;
                        }
                        var avgError = Math.Max(1, (originalRequirementError + newRequirementError) / 2);
                        var error = (originalRequirementError - newRequirementError);
                        const double errorMultiplier = 1e-4;
                        // When error is >0 we've improved the system, so encourage it.
                        coolness += errorMultiplier * error;
                    }
                    usefulnessWatch.Stop();

                    { // Less coolness to rooms that hog resources either for operation or for building
                    }
                    coolness += (float)SessionCore.RANDOM.NextDouble();
                    weightedRoomChoice.Add(room, (float)coolness);
                }
                finally
                {
                    c.RemoveRoom(room);
                }
            }
            if (Settings.Instance.DebugGenerationStages)
                SessionCore.Log("Choose from {0} valid options; generated weights in {1}.  Collision in {2}, inout in {3}, usefulness in {4}",
                weightedRoomChoice.Count, iwatch.Elapsed, collisionWatch.Elapsed, inoutFactorWatch.Elapsed, usefulnessWatch.Elapsed);

            if (weightedRoomChoice.Count == 0)
                return false;
            {
                // 50% chance to be in the top 10% of choices.
                var room = weightedRoomChoice.ChooseByQuantile(SessionCore.RANDOM.NextDouble(), 0.9);
                room.TakeOwnership(c);

                var newErrMux = new List<string>();
                var newError = c.ComputeErrorAgainstSeed(MyUtilities.LogToList(newErrMux));

                if (Settings.Instance.DebugGenerationStagesWeights)
                    for (var i = 0; i < Math.Min(newErrMux.Count, Math.Min(bestErrMux.Count, originalError.Count)); i++)
                    {
                        if (originalError[i].Equals(newErrMux[i]) && bestErrMux[i].Equals(newErrMux[i])) continue;
                        SessionCore.Log("Old {0}", originalError[i]);
                        SessionCore.Log("New {0}", newErrMux[i]);
                        SessionCore.Log("Best {0}", bestErrMux[i]);
                    }
                if (Settings.Instance.DebugGenerationStages)
                    SessionCore.Log("Added {0} (number {1}) at {2}. Sadness changed {3:e} => {4:e} = {5:e}. Best was {6} with {7:e} less", room.Part.Name, c.Rooms.Count(), room.BoundingBox.Center, originalRequirementError, newError, originalRequirementError - newError, bestRoom, newError - bestError);
                return true;
            }
        }
    }
}