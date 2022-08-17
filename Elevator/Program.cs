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
        List<IMyPistonBase> _pistons = new List<IMyPistonBase>();

        float _pistonMinLimit = 0.16f;
        float _pistonMaxLimit = 9.84f;

        float GetSpeed(float x) => -0.115f * x * x + 1.15f * x + 0.1118f;

        int _direction = -1;

        public Program()
        {
            GridTerminalSystem.GetBlockGroupWithName("Elevator Pistons").GetBlocksOfType(_pistons);

            _pistons.ForEach(p =>
            {
                p.MaxLimit = _pistonMaxLimit;
                p.MinLimit = _pistonMinLimit;
            });

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Toggle")
            {
                _direction = -_direction;
            }
            _pistons.ForEach(p => p.Velocity = _direction * GetSpeed(p.CurrentPosition));
        }
    }
}
