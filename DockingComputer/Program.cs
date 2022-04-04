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
        List<IMyGyro> _gyros = new List<IMyGyro>();

        MatrixD _connectorMatrix => _connector.WorldMatrix;
        MatrixD _targetMatrix = MatrixD.Zero;
        Vector3D _targetVector = Vector3D.Zero;
        Vector3D _rotationVector = Vector3D.Zero;
        Vector3D _gyroVector = Vector3D.Zero;

        string _targetName;

        string _dockingBroadcastTag = "DOCKING_LISTENER";
        string _hangarBroadcastTag = "HANGAR_LISTENER";
        string _unicastTag = "DOCKING_INFORMATION";

        double _dockingOffset = 1.85;

        double _angle = 0;

        bool _aligning = false;

        public Program()
        {
            var connectors = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(connectors, c => c.IsParkingEnabled && c.BlockDefinition.SubtypeName != "ConnectorSmall");
            if (connectors.Count != 1) throw new Exception($"Must have one parking connector, actual {connectors.Count}");
            _connector = connectors.First();

            var cockpits = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(cockpits, c => c.IsMainCockpit);
            if (cockpits.Count != 1) throw new Exception($"Must have one main cockpit, actual {cockpits.Count}");
            _cockpit = cockpits.First();
            _dockingLcd = _cockpit.GetSurface(0);

            GridTerminalSystem.GetBlocksOfType(_gyros);
            if (!_gyros.Any()) throw new Exception($"Must have a gyro");
            _gyros = _gyros.GroupBy(g => g.Orientation.Forward).OrderBy(g => g.Count()).First().ToList();

            _listener = IGC.UnicastListener;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Toggle Hangar")
            {
                HandleHangarTrigger();
            }
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Toggle Docking")
            {
                HandleDockingTrigger();
            }
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Toggle Auto")
            {
                HandleAutoTrigger();
            }
            if ((updateSource & UpdateType.Update10) > 0)
            {
                HandleMessages();
                CalculateTargetVector();
                CalculateRotationVector();
                SetGyros();
                TryDocking();
                _dockingLcd.WriteText(GetStatus());
            }
        }

        private void HandleAutoTrigger()
        {
            _aligning = !_aligning;
            _gyros.ForEach(g => g.GyroOverride = _aligning);
        }

        private void TryDocking()
        {
            if (_targetMatrix == MatrixD.Zero) return;
            if (_connector.Status == MyShipConnectorStatus.Connectable)
            {
                _connector.Connect();
                Shutdown();
            }
        }

        private void HandleHangarTrigger()
        {
            IGC.SendBroadcastMessage(_hangarBroadcastTag, _cockpit.WorldMatrix);
        }

        private void HandleDockingTrigger()
        {
            if (_targetMatrix == MatrixD.Zero)
            {
                IGC.SendBroadcastMessage(_dockingBroadcastTag, _connectorMatrix);
                return;
            }
            Shutdown();
        }

        private void Shutdown()
        {
            _targetMatrix = MatrixD.Zero;
            _gyros.ForEach(g => g.GyroOverride = false);
            _aligning = false;
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
                        var newMatrix = data.Item2;
                        newMatrix.Translation += newMatrix.Forward * _dockingOffset;
                        if (Distance(newMatrix) < Distance(_targetMatrix)) {
                            _targetName = data.Item1;
                            _targetMatrix = MatrixD.CreateFromDir(newMatrix.Backward);
                            _targetMatrix.Translation = newMatrix.Translation;
                        }
                    }
                }
            }
        }

        double Distance(MatrixD target)
        {
            return Vector3D.Distance(target.Translation, _connectorMatrix.Translation);
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
            QuaternionD target = QuaternionD.CreateFromRotationMatrix(_targetMatrix.GetOrientation());
            QuaternionD current = QuaternionD.CreateFromRotationMatrix(_connectorMatrix.GetOrientation());
            QuaternionD rotation = target / current;
            rotation.Normalize();
            Vector3D axis;
            rotation.GetAxisAngle(out axis, out _angle);

            MatrixD worldToCockpit = MatrixD.Invert(_cockpit.WorldMatrix.GetOrientation());
            MatrixD worldToGyro = MatrixD.Invert(_gyros.First().WorldMatrix.GetOrientation());
            Vector3D localAxis = Vector3D.Transform(axis, worldToCockpit);
            Vector3D localGyroAxis = Vector3D.Transform(axis, worldToGyro);

            double value = Math.Log(_angle + 1, 2);
            localAxis *= value < 0.001 ? 0 : value;
            _rotationVector = localAxis;
            localGyroAxis *= value < 0.001 ? 0 : value;
            _gyroVector = localGyroAxis;
        }



        void SetGyros()
        {
            if (!_aligning) return;
            _gyros.ForEach(g =>
            {
                g.Pitch = (float)-_gyroVector.X;
                g.Yaw = (float)-_gyroVector.Y;
                g.Roll = (float)-_gyroVector.Z;
            });
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
            status += $"Pitch:    {GetAngle(_rotationVector.X, "X")}\n";
            status += $"Yaw:      {GetAngle(_rotationVector.Y, "Y")}\n";
            status += $"Roll:     {GetAngle(_rotationVector.Z, "Z")}\n";
            return status;
        }

        string GetTranslation(double magnitude, string direction)
        {
            if (Math.Abs(magnitude) < 0.3) return "OK";
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

        string GetAngle(double magnitude, string direction)
        {
            if (Math.Abs(magnitude) < 0.1) return "OK";
            var trans = $"{Math.Abs(magnitude):0.0} ";
            switch (direction)
            {
                case "X":
                    return trans + (magnitude < 0 ? "Dwn" : " Up");
                case "Y":
                    return trans + (magnitude < 0 ? "Rgt" : "Lft");
                default:
                    return trans + (magnitude < 0 ? "Rgt" : "Lft");
            }
        }
    }
}
