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

        List<IMyThrust> _allThrusters = new List<IMyThrust>();
        List<IMyThrust> _upThrusters = new List<IMyThrust>();
        List<IMyThrust> _downThrusters = new List<IMyThrust>();

        MatrixD _connectorMatrix => _connector.WorldMatrix;
        MatrixD _targetMatrix = MatrixD.Zero;
        Vector3D _targetVector = Vector3D.Zero;
        Vector3D _startVector = Vector3D.Zero;
        Vector3D _rotationVector = Vector3D.Zero;
        Vector3D _gyroVector = Vector3D.Zero;

        Vector3D _dockVector => _targetMatrix.Backward;
        Vector3D _connectorVector => _connectorMatrix.Forward;

        PID _x;
        PID _y;
        PID _z;
        const double Timestep = 10.0 / 60;

        string _targetName;

        string _dockingBroadcastTag = "DOCKING_LISTENER";
        string _unicastTag = "DOCKING_INFORMATION";

        double _dockingOffset = 1.85;

        double _angle = 0;
        float _xx = 0;
        bool _auto = false;
        bool _translating = false;

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
            GridTerminalSystem.GetBlocksOfType(_allThrusters);
            GridTerminalSystem.GetBlocksOfType(_upThrusters, t => t.Orientation.Forward == _cockpit.Orientation.Up);
            GridTerminalSystem.GetBlocksOfType(_downThrusters, t => t.Orientation.Forward == _cockpit.Orientation.Up + 1);

            _x = new PID(1, 0, 0, Timestep);
            _y = new PID(1, 0, 0, Timestep);
            _z = new PID(1, 0, 0, Timestep);


            _listener = IGC.UnicastListener;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }


        public void Main(string argument, UpdateType updateSource)
        {
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
            _auto = !_auto;
            _gyros.ForEach(g => g.GyroOverride = _auto);
            _startVector = _targetVector;
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
            _auto = false;
            _translating = false;
            _gyros.ForEach(g => g.GyroOverride = false); 
            _allThrusters.ForEach(t => t.ThrustOverridePercentage = 0);
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
            if (!_auto) return;
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
            status += $"test: {_angle}\n";
            status += $"test: {_xx}\n";
            status += $"test: {_upThrusters.Except(_downThrusters).Any()}\n";
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
