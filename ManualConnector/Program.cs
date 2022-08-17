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

        IMyPistonBase[] Pistons = new IMyPistonBase[3];
        IMyShipConnector Connector;
        IMyShipController Helm;
        IMyTextSurface Lcd;

        public Program()
        {
            Connector = GridTerminalSystem.GetBlockWithName("Manual Connector") as IMyShipConnector;
            Helm = GridTerminalSystem.GetBlockWithName("Connector Helm") as IMyShipController;
            Pistons[0] = GridTerminalSystem.GetBlockWithName("Connector Piston Up") as IMyPistonBase;
            Pistons[1] = GridTerminalSystem.GetBlockWithName("Connector Piston Left") as IMyPistonBase;
            Pistons[2] = GridTerminalSystem.GetBlockWithName("Connector Piston Forward") as IMyPistonBase;

            Lcd = (Helm as IMyTextSurfaceProvider).GetSurface(1);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            var v = Helm.MoveIndicator;
            Lcd.WriteText($"{v}");

            Pistons[0].Velocity = v.Y;
            Pistons[1].Velocity = -v.X;
            Pistons[2].Velocity = -v.Z;
        }
    }
}
