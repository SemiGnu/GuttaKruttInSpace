using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        IMyMotorStator _miningHinge;
        IMyMotorStator _indicatorHinge;

        List<IMyPistonBase> _pistons = new List<IMyPistonBase>();
        List<IMyPistonBase> _revPistons = new List<IMyPistonBase>();

        const float Pi = (float)Math.PI;
        const float QuarterAngle = Pi / 4f;

        public Program()
        {
            _miningHinge = GridTerminalSystem.GetBlockWithName($"Mining Hinge") as IMyMotorStator;
            _indicatorHinge = GridTerminalSystem.GetBlockWithName($"Indicator Hinge") as IMyMotorStator;

            GridTerminalSystem.GetBlocksOfType(_pistons);
            GridTerminalSystem.GetBlocksOfType(_revPistons, p => p.CustomName.Contains("R"));
            _pistons = _pistons.Except(_revPistons).ToList();

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var fwd = Math.Max(0f, _indicatorHinge.Angle - QuarterAngle);
            var bwd = Math.Min(0f, _indicatorHinge.Angle + QuarterAngle);
            var vel = (fwd + bwd) * 10 / Pi;
            _pistons.ForEach(p => p.Velocity = vel);
            _revPistons.ForEach(p => p.Velocity = -vel);
        }
    }
}
