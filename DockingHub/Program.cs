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
        //IMyMotorStator _hinge;
        IMyTextSurface _lcd;
        List<IMyShipConnector> _connectors;

        IMyBroadcastListener _listener;

        string _lastSent;

        string _broadcastTag = "DOCKING_LISTENER";
        string _unicastTag = "DOCKING_INFORMATION";

        public Program()
        {
            //_hinge = GridTerminalSystem.GetBlockWithName("Hinge - Radar Dish") as IMyMotorStator;
            _lcd = Me.GetSurface(0);

            _connectors = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(_connectors, c => c.CubeGrid == Me.CubeGrid);

            _listener = IGC.RegisterBroadcastListener(_broadcastTag);
            _listener.SetMessageCallback(_broadcastTag);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            Echo(_lastSent);
            PrintStatus();
            HandleMessages();
        }

        private void HandleMessages()
        {
            while (_listener.HasPendingMessage)
            {
                MyIGCMessage message = _listener.AcceptMessage();
                if (message.Tag == _broadcastTag)
                {
                    if (message.Data is MatrixD)
                    {
                        var closestConnector = GetClosestConnector((MatrixD)message.Data);
                        IGC.SendUnicastMessage(message.Source, _unicastTag, MyTuple.Create(closestConnector.CustomName, closestConnector.WorldMatrix));
                        _lastSent = closestConnector.CustomName;
                    }
                }
            }
        }

        IMyShipConnector GetClosestConnector(MatrixD target)
        {
            return _connectors
                .Where(c => c.Status == MyShipConnectorStatus.Unconnected)
                .OrderBy(c => (c.WorldMatrix.Translation - target.Translation).Length())
                .FirstOrDefault();
        }

        private void PrintStatus()
        {
            //var status = GetStatus();
            //_lcd.WriteText($"{status}");
        }

        //private string GetStatus()
        //{
        //    var angle = _hinge.Angle * (180 / Math.PI);
        //    var speed = _hinge.TargetVelocityRPM * 6;

        //    if (angle > 45) _hinge.TargetVelocityRPM = -1f;
        //    if (angle < -45) _hinge.TargetVelocityRPM = 1f;

        //    var status = "Radar Dish Status\n";
        //    status += $"Dish Angle: {Math.Abs(angle):00}° {(angle < 0 ? "left" : "right")}\n";
        //    status += $"Panning {(speed < 0 ? "left" : "right")}\n";
        //    status += $"  at {Math.Abs(speed):0} °/s";

        //    return status;
        //}

    }
}
