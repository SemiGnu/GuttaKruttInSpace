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
        IMyTextSurface _dockingLcd;
        IMyUnicastListener _listener;

        MatrixD _targetWorldMatrix = MatrixD.Zero;
        double _targetDistance = double.NaN;

        string _dockingBroadcastTag = "DOCKING_LISTENER";
        string _unicastTag = "DOCKING_INFORMATION";

        public Program()
        {
            _connector = GridTerminalSystem.GetBlockWithName("Drill Connector") as IMyShipConnector;
            var cockpit = GridTerminalSystem.GetBlockWithName("Drill Cockpit") as IMyCockpit;
            _dockingLcd = cockpit.GetSurface(1);

            _listener = IGC.UnicastListener;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Request Docking")
            {
                var status = $"Sending matrix\n{_connector.WorldMatrix}";
                Echo(status);
                _dockingLcd.WriteText(status);
                IGC.SendBroadcastMessage(_dockingBroadcastTag, _connector.WorldMatrix);
            }
            if ((updateSource & UpdateType.IGC) > 0)
            {
                _dockingLcd.WriteText("Message Rceived");
                HandleMessages();
            }
            if ((updateSource & UpdateType.Update10) > 0)
            {
                CalculateDistance();
            }
        }

        private void HandleMessages()
        {
            while (_listener.HasPendingMessage)
            {
                MyIGCMessage message = _listener.AcceptMessage();
                _dockingLcd.WriteText("Message Accepted");
                if (message.Tag == _unicastTag)
                {
                    _dockingLcd.WriteText("Message Tag Validated", true);
                    if (message.Data is MyTuple<string, MatrixD>)
                    {
                        _dockingLcd.WriteText("Message Type Validated", true);
                        var data = (MyTuple<string, MatrixD>)message.Data;
                        _dockingLcd.WriteText($"Item 1: {data.Item1}", true);
                        _dockingLcd.WriteText($"Item 2: {data.Item2}", true);
                        _targetWorldMatrix = data.Item2;
                    }
                }
            }
        }

        private void CalculateDistance()
        {
            if (_targetWorldMatrix == MatrixD.Zero) return;
            var distance = _targetWorldMatrix.Translation - _connector.WorldMatrix.Translation;
            _targetDistance = distance.Length();
        }
    }
}
