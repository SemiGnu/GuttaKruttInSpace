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
        IMyShipConnector _connector;
        double _offset => _connector.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 1.85 : 0.65;
        const string _autoConnectorTagBroadcastTag = "AUTO_CONNECTOR_LISTENER";


        string _status = "idle";

        public Program()
        {
            _connector = GridTerminalSystem.GetBlockWithName("Auto Connector") as IMyShipConnector;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo(_status);
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Dock")
            {
                var matrix = GetMatrix();
                IGC.SendBroadcastMessage(_autoConnectorTagBroadcastTag, matrix);
                _status = $"{matrix}";
            }
        }


        private MatrixD GetMatrix()
        {
            var translation = _connector.WorldMatrix.Translation + _connector.WorldMatrix.Forward * _offset;
            var matrix = _connector.WorldMatrix;
            matrix.Translation = translation;
            return matrix;
        }
    }
}
