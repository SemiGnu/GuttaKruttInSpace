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
        IMyPistonBase[] _pistons = new IMyPistonBase[3];

        float[] _positions = new float[3];

        Status _status = Status.Idle;
        bool IsDrilling => (_status & Status.Drilling) > 0 && _pistons[In].CurrentPosition > 0.1;

        const int Up = 0;
        const int Right = 1;
        const int In = 2;

        const float StepSize = 2.5f;

        public Program()
        {
            _pistons[Up] = GridTerminalSystem.GetBlockWithName("Excavator Up") as IMyPistonBase;
            _pistons[Right] = GridTerminalSystem.GetBlockWithName("Excavator Right") as IMyPistonBase;
            _pistons[In] = GridTerminalSystem.GetBlockWithName("Excavator In") as IMyPistonBase;

            var restore = Storage ?? "0,0,0,0";
            _positions = restore.Split(',').Take(3).Select(float.Parse).ToArray();
            _status = (Status)int.Parse(restore.Split(',').Last());

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
            Storage = string.Join(",", _positions.Select(p => p.ToString()));
            Storage += $",{(int)_status}";
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (_status == Status.Idle)
            {
                _status = Status.Drilling;
                _pistons[In].Velocity = 0.5f;
            }
            if (_status == Status.Drilling && _pistons[In].CurrentPosition >= 10) 
            {
                _pistons[In].Velocity = -0.5f;
            }
            if (_status == Status.Drilling && _pistons[In].Velocity < 0 && _pistons[In].CurrentPosition > 0.1)
            {
                _status = Status.Translating;
            }
            if (_status = Status.Translating &&)

        }


        enum Status
        {
            Idle = 0,
            Translating = 1,
            Drilling =
        }

    }
}
