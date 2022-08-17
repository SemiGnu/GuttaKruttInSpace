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
        List<IMyShipConnector> _connectors = new List<IMyShipConnector>();
        List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>();
        List<IMyGasTank> _gasTanks = new List<IMyGasTank>();
        List<IMyThrust> _thrusters = new List<IMyThrust>();
        List<IMyGyro> _gyros = new List<IMyGyro>();

        public Program()
        {
            GridTerminalSystem.GetBlocksOfType(_connectors, t => t.CubeGrid == Me.CubeGrid);
            GridTerminalSystem.GetBlocksOfType(_batteries, t => t.CubeGrid == Me.CubeGrid);
            GridTerminalSystem.GetBlocksOfType(_gasTanks, t => t.CubeGrid == Me.CubeGrid);
            GridTerminalSystem.GetBlocksOfType(_thrusters, t => t.CubeGrid == Me.CubeGrid);
            GridTerminalSystem.GetBlocksOfType(_gyros, t => t.CubeGrid == Me.CubeGrid);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var isConnected = _connectors.Any(c => c.Status == MyShipConnectorStatus.Connected);

            _batteries.ForEach(b => b.ChargeMode = isConnected ? ChargeMode.Recharge : ChargeMode.Auto);
            _gasTanks.ForEach(g => g.Stockpile = isConnected);
            _thrusters.ForEach(t => t.Enabled = !isConnected);
            _gyros.ForEach(g => g.Enabled = !isConnected);
        }
    }
}
