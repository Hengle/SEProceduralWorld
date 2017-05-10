using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Construction;
using ProcBuild.Creation;
using ProcBuild.Generation;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace ProcBuild
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    internal class SessionCore : MySessionComponentBase
    {
        public static SessionCore Instance { get; private set; }

        public static void Log(string format, params object[] args)
        {
            if (Instance?.Logger != null)
                Instance.Logger.Log(format, args);
            else
                MyLog.Default?.Log(MyLogSeverity.Info, format, args);
        }

        public static void LogBoth(string fmt, params object[] args)
        {
            SessionCore.Log(fmt, args);
            MyAPIGateway.Utilities.ShowMessage("Exporter", string.Format(fmt, args));
        }

        public static readonly Random RANDOM = new Random();

        public override void LoadData()
        {
            base.LoadData();
        }

        private bool m_attached = false;
        public MyPartManager PartManager { get; private set; }
        public Logging Logger { get; private set; }
        public Settings Settings { get; private set; }

        private List<MyTuple<MyProceduralConstruction, IMyCubeGrid>> procgen = new List<MyTuple<MyProceduralConstruction, IMyCubeGrid>>();
        private List<MyTuple<MyProceduralConstruction, IMyCubeGrid>> debugParts = new List<MyTuple<MyProceduralConstruction, IMyCubeGrid>>();


        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            if (MyAPIGateway.Session == null) return;
            if (MyAPIGateway.Session.Player == null) return;
            if (!m_attached)
                Attach();
            Logger.OnUpdate();

            var blocks = Color.Red;
            var reserved = new[] { Color.Blue, Color.Aqua, Color.Violet, Color.Cyan };
            var allReserved = Color.Green;
            var mountPointColor = Color.HotPink;

            // ReSharper disable once InvertIf
            if (Settings.DebugModuleAABB | Settings.DebugModuleReservedAABB)
                foreach (var f in procgen)
                {
                    var transform = f.Item2.WorldMatrix;
                    var gridSize = f.Item2.GridSize;
                    foreach (var room in f.Item1.Rooms)
                    {
                        if (Settings.DebugModuleAABB)
                        {
                            var localAABB = new BoundingBoxD(room.BoundingBox.Min * gridSize, room.BoundingBox.Max * gridSize);
                            MySimpleObjectDraw.DrawTransparentBox(ref transform, ref localAABB, ref blocks, MySimpleObjectRasterizer.Wireframe, 1, .02f);
                            if (room.Part.ReservedSpaces.Any())
                            {
                                var temp = MyUtilities.TransformBoundingBox(room.Part.ReservedSpace, room.Transform);
                                var tmpAABB = new BoundingBoxD(temp.Min * gridSize, temp.Max * gridSize);
                                MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref allReserved, MySimpleObjectRasterizer.Wireframe, 1, .02f);
                            }
                        }
                        if (Settings.DebugModuleReservedAABB)
                            foreach (var rs in room.Part.ReservedSpaces)
                            {
                                var temp = MyUtilities.TransformBoundingBox(rs.Box, room.Transform);
                                var tmpAABB = new BoundingBoxD(temp.Min * gridSize, temp.Max * gridSize);
                                MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref reserved[(rs.IsShared ? 1 : 0) + (rs.IsOptional ? 2 : 0)], MySimpleObjectRasterizer.Wireframe, 1, .005f);
                            }
                    }
                }

            foreach (var f in debugParts)
            {
                var transform = f.Item2.WorldMatrix;
                var gridSize = f.Item2.GridSize;
                foreach (var room in f.Item1.Rooms)
                {
                    var localAABB = new BoundingBoxD(room.BoundingBox.Min * gridSize, (room.BoundingBox.Max+1) * gridSize);
                    MySimpleObjectDraw.DrawTransparentBox(ref transform, ref localAABB, ref blocks, MySimpleObjectRasterizer.Wireframe, 1, .02f);
                    if (room.Part.ReservedSpaces.Any())
                    {
                        var temp = MyUtilities.TransformBoundingBox(room.Part.ReservedSpace, room.Transform);
                        var tmpAABB = new BoundingBoxD(temp.Min * gridSize, (temp.Max+1) * gridSize);
                        MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref allReserved, MySimpleObjectRasterizer.Wireframe, 1, .02f);
                    }
                    foreach (var rs in room.Part.ReservedSpaces)
                    {
                        var temp = MyUtilities.TransformBoundingBox(rs.Box, room.Transform);
                        var tmpAABB = new BoundingBoxD(temp.Min * gridSize, (temp.Max+1) * gridSize);
                        tmpAABB = tmpAABB.Inflate(-0.02);
                        MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref reserved[(rs.IsShared ? 1 : 0) + (rs.IsOptional ? 2 : 0)], MySimpleObjectRasterizer.Wireframe, 1, .005f);
                    }
                    foreach (var mount in room.MountPoints)
                    {
                        foreach (var block in mount.MountPoint.Blocks)
                        {
                            var anchorLoc = (Vector3)block.AnchorLocation;
                            var opposeLoc = (Vector3)(block.AnchorLocation + (block.MountDirection * 2));
                            opposeLoc += 0.5f * Vector3.Abs(Vector3I.One - Vector3I.Abs(block.MountDirection)); // hacky way to get perp. components
                            anchorLoc -= 0.5f * Vector3.Abs(Vector3I.One - Vector3I.Abs(block.MountDirection));

                            var anchor = gridSize * Vector3.Transform(anchorLoc, room.Transform.GetFloatMatrix());
                            var oppose = gridSize * Vector3.Transform(opposeLoc, room.Transform.GetFloatMatrix());
                            var tmpAABB = new BoundingBoxD(Vector3.Min(anchor, oppose), Vector3.Max(anchor, oppose));
                            MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref mountPointColor, MySimpleObjectRasterizer.Solid, 1, .02f);
                        }
                    }
                }
            }
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            if (m_attached)
                Detach();
        }

        private void Attach()
        {
            m_attached = true;
            Instance = this;
            try
            {
                Settings = new Settings();
                Logger = new Logging("ProceduralBuilding.log");
                PartManager = new MyPartManager();
                PartManager.LoadAll();
                foreach (var item in PartManager)
                    MyAPIGateway.Utilities.ShowMessage("Parts", item.Prefab.Id.SubtypeName);

                MyAPIGateway.Utilities.ShowNotification("Attached procedural building");
                MyAPIGateway.Utilities.MessageEntered += CommandDispatcher;
            }
            catch (Exception e)
            {
                Log("Fatal error loading Procedural Buildings: \n{0}", e);
            }
        }

        private void Detach()
        {
            m_attached = false;

            MyAPIGateway.Utilities.MessageEntered -= CommandDispatcher;

            Logger?.Close();
            Logger = null;
            PartManager = null;
            Instance = null;
        }

        private static string FormatMatrix(MatrixI m2)
        {
            var m = m2.GetFloatMatrix();
            return $"{m.M11,5} {m.M12,5} {m.M13,5} {m.Translation.X,5}\r\n{m.M21,5} {m.M22,5} {m.M23,5} {m.Translation.Y,5}\r\n{m.M31,5} {m.M32,5} {m.M33,5} {m.Translation.Z,5}";
        }

        private void CommandDispatcher(string messageText, ref bool sendToOthers)
        {
            if (!MyAPIGateway.Session.IsServer || !messageText.StartsWith("/")) return;
            var args = messageText.Split(' ');
            if (args[0].Equals("/export"))
            {
                MyAPIGateway.Entities.GetEntities(null, x =>
                {
                    var grid = x as IMyCubeGrid;
                    if (grid != null)
                        MyDesignTool.Process(grid);
                    return false;
                });
                return;
            }
            if (args[0].Equals("/part"))
                ProcessDebugPart(args);
            if (args[0].Equals("/list"))
                ProcessSpawn(args);
        }

        private void ProcessDebugPart(IReadOnlyList<string> args)
        {
            if (args.Count < 2)
            {
                MyAPIGateway.Utilities.ShowMessage("PDebug", "Usage: " + args[0] + " [part name]");
                return;
            }
            var part = PartManager.FirstOrDefault(test => test.Prefab.Id.SubtypeName.ToLower().Contains(args[1].ToLower()));
            if (part == null)
            {
                MyAPIGateway.Utilities.ShowMessage("PDebug", "Unable to find part with name \"" + args[1] + "\"");
                return;
            }
            var c = new MyProceduralConstruction();
            c.GenerateRoom(new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up), part);
            IMyCubeGrid dest = null;
            var iwatch = new Stopwatch();
            foreach (var room in c.Rooms)
            {
                iwatch.Restart();
                if (dest == null)
                {
                    var temp = MyAPIGateway.Session.Player.Character.WorldMatrix;
                    var basePos = room.Part.PrimaryGrid.PositionAndOrientation?.AsMatrixD();
                    if (basePos.HasValue)
                    {
                        var copy = basePos.Value;
                        copy.Translation -= room.BoundingBox.Center * 2.5f;
                        temp = MatrixD.Multiply(copy, temp);
                    }
                    dest = MyGridCreator.SpawnRoomAt(room, temp);
                }
                else
                    MyGridCreator.AppendRoom(dest, room);
                Log("Added room {3} of {0} blocks with {1} aux grids in {2}", room.Part.PrimaryGrid.CubeBlocks.Count, room.Part.Prefab.CubeGrids.Length - 1, iwatch.Elapsed, room.Part.Name);
            }
            debugParts.Add(MyTuple.Create(c, dest));
        }

        private void ProcessSpawn(IReadOnlyList<string> args)
        {
            try
            {
                var count = 2;
                if (args.Count >= 2)
                    int.TryParse(args[1].Trim(), out count);

                var watch = new Stopwatch();
                watch.Reset();
                watch.Start();

                var construction = new MyProceduralConstruction();
                {
                    // Seed the generator
                    var part = PartManager.First();
                    if (args.Count >= 3)
                        foreach (var test in PartManager)
                            if (test.Prefab.Id.SubtypeName.ToLower().Contains(args[2].ToLower()))
                            {
                                part = test;
                                break;
                            }
                    construction.GenerateRoom(new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up), part);
                }

                for (var i = 0; i < count; i++)
                    if (!MyGenerator.StepConstruction(construction, (i > count / 2) ? 0 : 1))
                        break;
                // Give it plenty of tries to close itself
                {
                    var remainingMounts = construction.Rooms.SelectMany(x => x.MountPoints).Count(y => y.AttachedTo == null);
                    var triesToClose = remainingMounts * 2 + 2;
                    Log("There are {0} remaining mounts.  Giving it {1} tries to close itself.", remainingMounts, triesToClose);
                    var outOfOptions = false;
                    for (var i = 0; i < triesToClose; i++)
                        if (!MyGenerator.StepConstruction(construction, -10))
                        {
                            outOfOptions = true;
                            break;
                        }
                    remainingMounts = construction.Rooms.SelectMany(x => x.MountPoints).Count(y => y.AttachedTo == null);
                    if (remainingMounts > 0)
                    {
                        Log("Now there are {0} remaining mounts.  Trying without hints. Reason: {1}", remainingMounts, outOfOptions ? "Out of options" : "Out of tries");
                        triesToClose = remainingMounts * 2 + 2;
                        for (var i = 0; i < triesToClose; i++)
                            if (!MyGenerator.StepConstruction(construction, -10, false))
                            {
                                outOfOptions = true;
                                break;
                            }
                    }
                    remainingMounts = construction.Rooms.SelectMany(x => x.MountPoints).Count(y => y.AttachedTo == null);
                    if (remainingMounts > 0)
                        Log("Now there are {0} remaining mounts.  Reason: {1}", remainingMounts, outOfOptions ? "Out of options" : "Out of tries");
                    else
                        Log("Sucessfully closed all mount points");
                }

                var generate = watch.Elapsed;
                IMyCubeGrid dest = null;
                var iwatch = new Stopwatch();
                watch.Restart();
                foreach (var room in construction.Rooms)
                {
                    iwatch.Restart();
                    if (dest == null)
                    {
                        var temp = MyAPIGateway.Session.Player.Character.WorldMatrix;
                        var basePos = room.Part.PrimaryGrid.PositionAndOrientation?.AsMatrixD();
                        if (basePos.HasValue)
                        {
                            var copy = basePos.Value;
                            copy.Translation -= room.BoundingBox.Center * 2.5f;
                            temp = MatrixD.Multiply(copy, temp);
                        }
                        dest = MyGridCreator.SpawnRoomAt(room, temp);
                    }
                    else
                        MyGridCreator.AppendRoom(dest, room);
                    Log("Added room {3} of {0} blocks with {1} aux grids in {2}", room.Part.PrimaryGrid.CubeBlocks.Count, room.Part.Prefab.CubeGrids.Length - 1, iwatch.Elapsed, room.Part.Name);
                }
                var msg = $"Added {construction.Rooms.Count()} rooms; generated in {generate}, added in {watch.Elapsed}";
                Log(msg);
                MyAPIGateway.Utilities.ShowMessage("Gen", msg);
                procgen.Add(MyTuple.Create(construction, dest));
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
            }
        }
    }
}
