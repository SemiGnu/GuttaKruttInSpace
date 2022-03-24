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
        IMyCockpit _cockpit;
        IMyTextSurface _dockingLcd;
        IMyUnicastListener _listener;

        MatrixD _connectorMatrix => _connector.WorldMatrix;
        MatrixD _targetMatrix = MatrixD.Zero;
        Vector3D _targetVector = Vector3D.Zero;
        Vector3D _rotationVector = Vector3D.Zero;

        string _targetName;

        string _dockingBroadcastTag = "DOCKING_LISTENER";
        string _unicastTag = "DOCKING_INFORMATION";

        double _dockingOffset = 1.85;

        public Program()
        {
            _connector = GridTerminalSystem.GetBlockWithName("Drill Connector") as IMyShipConnector;
            _cockpit = GridTerminalSystem.GetBlockWithName("Drill Cockpit") as IMyCockpit;
            _dockingLcd = _cockpit.GetSurface(1);

            _listener = IGC.UnicastListener;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Toggle Docking")
            {
                HandleDockingTrigger();
            }
            if ((updateSource & UpdateType.Update10) > 0)
            {
                HandleMessages();
                CalculateTargetVector();
                CalculateRotationVector();
                TryDocking();
                _dockingLcd.WriteText(GetStatus());
            }
        }

        private void TryDocking()
        {
            if (_targetMatrix == MatrixD.Zero) return;
            if (_connector.Status == MyShipConnectorStatus.Connectable)
            {
                _connector.Connect();
                _targetMatrix = MatrixD.Zero;
            }
        }

        private void HandleDockingTrigger()
        {
            if (_targetMatrix == MatrixD.Zero)
            {
                IGC.SendBroadcastMessage(_dockingBroadcastTag, _connectorMatrix);
                return;
            }
            _targetMatrix = MatrixD.Zero;
        }

        private void HandleMessages()
        {
            while (_listener.HasPendingMessage)
            {
                MyIGCMessage message = _listener.AcceptMessage();
                if (message.Tag == _unicastTag)
                {
                    if (message.Data is MyTuple<string, MatrixD>)
                    {
                        var data = (MyTuple<string, MatrixD>)message.Data;
                        _targetName = data.Item1;
                        _targetMatrix = data.Item2;
                        _targetMatrix.Translation += _targetMatrix.Forward * _dockingOffset;
                    }
                }
            }
        }

        private void CalculateTargetVector()
        {
            if (_targetMatrix == MatrixD.Zero) return;
            var distance = _targetMatrix.Translation - _connectorMatrix.Translation;
            _targetVector = Vector3D.TransformNormal(distance, MatrixD.Transpose(_cockpit.WorldMatrix));
        }

        private void CalculateRotationVector()
        {
            if (_targetMatrix == MatrixD.Zero) return;
            //_rotationVector = _targetMatrix.Forward - _connector.WorldMatrix.Backward;
            //_rotationVector = Vector3D.TransformNormal(_targetMatrix.Forward - _connector.WorldMatrix.Backward, MatrixD.Transpose(_cockpit.WorldMatrix));
            var target = Quaternion.CreateFromRotationMatrix(_targetMatrix.GetOrientation());
            var self = Quaternion.CreateFromRotationMatrix(_connectorMatrix.GetOrientation());
            var rotation = target / self;
            Vector3 axis;
            float angle;
            rotation.GetAxisAngle(out axis, out angle);
            _rotationVector = axis;
        }

        private string GetStatus() 
        {
            var status = "Docking computer\n\n";
            if (_connector.Status == MyShipConnectorStatus.Connected)
            {
                status += $"Docked at\n{_connector.OtherConnector.CubeGrid.CustomName}";
                return status;
            }
            if (_targetMatrix == MatrixD.Zero)
            {
                status += "Offline";
                return status;
            }
            status += $"Target:   {_targetName}\n";
            status += $"Tanslate: {GetTranslation(_targetVector.X,"X")}\n";
            status += $"Tanslate: {GetTranslation(_targetVector.Y,"Y")}\n";
            status += $"Tanslate: {GetTranslation(_targetVector.Z,"Z")}\n";
            status += "Rotational controls\nmalfunctioning\n";
            status += $"Pitch:    {_rotationVector.X:0.00} Up\n";
            status += $"Pitch:    {_rotationVector.X:0.00} Up\n";
            status += $"Roll:     {_rotationVector.Z:0.00} Rgt\n";
            return status;
        }

        string GetTranslation(double magnitude, string direction)
        {
            if (Math.Abs(magnitude) < 0.5) return "OK";
            var trans = $"{Math.Abs(magnitude):0.0} ";
            switch (direction)
            {
                case "X":
                    return trans + (magnitude > 0 ? "Rgt" : "Lft");
                case "Y":
                    return trans + (magnitude > 0 ? " Up" : "Dwn");
                default:
                    return trans + (magnitude > 0 ? "Bwd" : "Fwd");
            }
        }

    }
}
