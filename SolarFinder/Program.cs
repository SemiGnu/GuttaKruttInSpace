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

        List<Finder> _finders;
        IMyTextSurface _lcd;

        public Program()
        {
            var axes = new[] { "X", "Y", "Z" };

            _finders = axes.Select(a => new Finder(GridTerminalSystem, a)).ToList();

            _lcd = Me.GetSurface(0);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _finders.ForEach(f => f.Main());

            var statuses = _finders.Select(f => $"{f.Axis}: {f.Angle / 2 / Math.PI * 360:0}");

            _lcd.WriteText($"{string.Join("\n", statuses)}");
        }
    }
}
