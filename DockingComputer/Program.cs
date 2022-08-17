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
        MyIni _ini = new MyIni();

        IMyShipConnector _connector;
        List<IMyCockpit> _cockpits = new List<IMyCockpit>();
        List<IMyTextSurface> _dockingLcds = new List<IMyTextSurface>();
        List<IMyGyro> _gyros = new List<IMyGyro>();
        List<IMyThrust> _thrusters = new List<IMyThrust>();
        List<IMyThrust>[] _thrusterArray = new List<IMyThrust>[6];

        IMyCockpit ActiveCockpit => _cockpits.FirstOrDefault(c => c.IsUnderControl) ?? _cockpits.First();

        MatrixD _connectorMatrix => _connector.WorldMatrix;
        MatrixD _targetMatrix = MatrixD.Zero;
        QuaternionD _targetQuaternion = QuaternionD.Identity;
        Vector3D _targetVector = Vector3D.Zero;
        Vector3D _rotationVector = Vector3D.Zero;
        Vector3D _gyroVector = Vector3D.Zero;

        string _targetName;

        IMyUnicastListener _listener;
        string _dockingBroadcastTag = "DOCKING_LISTENER";
        string _hangarBroadcastTag = "HANGAR_LISTENER";
        string _unicastTag = "DOCKING_INFORMATION";

        double _dockingOffset = 1.85;
        //double _dockingOffset = 15;

        double _angle = 0;

        bool _aligning = false;
        bool _inGravity = false;

        string _echo;

        static double _pidTimestep = 1.0 / 6.0;
        PID _yPid = new PID(1,0,1, _pidTimestep);
        PID _xPid = new PID(1,0,1, _pidTimestep);
        PID _zPid = new PID(1,0,1, _pidTimestep);

        public Program()
        {
            var connectors = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(connectors, c => c.IsParkingEnabled && c.BlockDefinition.SubtypeName != "ConnectorSmall");
            if (connectors.Count != 1) throw new Exception($"Must have one parking connector, actual {connectors.Count}");
            _connector = connectors.First();

            GridTerminalSystem.GetBlocksOfType(_cockpits, c => c.CustomData.Contains("DockingComputer"));
            if (!_cockpits.Any()) throw new Exception($"Must have at least one cockpit with [DockingComputer] custom data.");
            foreach (var c in _cockpits)
            {
                var surfaceIndex = CustomDataHelper.GetInt(c.CustomData, "DockingComputer", "Surface");
                _dockingLcds.Add(c.GetSurface(surfaceIndex));
            }

            GridTerminalSystem.GetBlocksOfType(_gyros);
            if (!_gyros.Any()) throw new Exception($"Must have a gyro");
            _gyros = _gyros.GroupBy(g => g.Orientation.Forward).OrderBy(g => g.Count()).First().ToList();


            _thrusterArray = _thrusterArray.Select(ta => new List<IMyThrust>()).ToArray();
            var allThrusters = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(allThrusters);
            var cDir = _cockpits.First().Orientation.Forward;
            foreach (var thruster in allThrusters)
            {
                var thrusterDirection = ActiveCockpit.Orientation.TransformDirectionInverse(thruster.Orientation.Forward);
                //var test2 = thruster.Orientation.TransformDirectionInverse(_cockpits.First().Orientation.Forward);
                _thrusterArray[(int)thrusterDirection].Add(thruster);
            }

            _listener = IGC.UnicastListener;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            _echo = "";
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
                _echo += $"aligning: {_aligning}\n";
                HandleMessages();
                CalculateTargetVector();
                CalculateRotationVector();
                SetGyros();
                SetIndicators();
                TryDocking();
                var status = GetStatus();
                _dockingLcds.ForEach(l => l.WriteText(status));

                Echo(_echo);
            }
        }

        private void Save()
        {
            Shutdown();
        }

        private void HandleAutoTrigger()
        {
            if (_targetMatrix == MatrixD.Zero) return;
            _aligning = !_aligning;
            _gyros.ForEach(g => g.GyroOverride = _aligning);
            //_cockpits.ForEach(c => c.ControlThrusters = !_aligning);
            if (!_aligning)
            {
                _thrusterArray.ToList().ForEach(ta => ta.ForEach(t => t.ThrustOverride = 0));
            }
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
            IGC.SendBroadcastMessage(_hangarBroadcastTag, ActiveCockpit.WorldMatrix);
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
            _aligning = false;
            _inGravity = false;
            _gyros.ForEach(g => g.GyroOverride = false);
            //_cockpits.ForEach(c => c.ControlThrusters = true);
            _thrusterArray.ToList().ForEach(ta => ta.ForEach(t => t.ThrustOverride = 0));
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
                            _targetQuaternion = QuaternionD.CreateFromRotationMatrix(_targetMatrix.GetOrientation());
                            var gravity = ActiveCockpit.GetNaturalGravity();
                            _inGravity = gravity != Vector3D.Zero;
                            if (_inGravity)
                            {
                                var connectorDirection = ActiveCockpit.Orientation.TransformDirectionInverse(_connector.Orientation.Forward);
                                var gravityAlign = QuaternionD.Identity;
                                switch (connectorDirection)
                                {
                                    case Base6Directions.Direction.Forward:
                                        var test = _targetMatrix.GetDirectionVector(Base6Directions.Direction.Up) + gravity;
                                        test.Normalize();
                                        break;
                                    default:
                                        throw new Exception("connector can't be that way!");
                                }
                                _targetQuaternion *= gravityAlign;
                            }
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
            _targetVector = Vector3D.TransformNormal(distance, MatrixD.Transpose(ActiveCockpit.WorldMatrix));
        }

        private void CalculateRotationVector()
        {
            if (_targetMatrix == MatrixD.Zero) return;
            QuaternionD current = QuaternionD.CreateFromRotationMatrix(_connectorMatrix.GetOrientation());
            QuaternionD rotation = _targetQuaternion / current;
            rotation.Normalize();
            Vector3D axis;
            rotation.GetAxisAngle(out axis, out _angle);

            MatrixD worldToCockpit = MatrixD.Invert(ActiveCockpit.WorldMatrix.GetOrientation());
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
                g.Pitch = (float) -_gyroVector.X;
                g.Yaw = (float)-_gyroVector.Y;
                g.Roll = (float)-_gyroVector.Z;
            });
        }

        void SetIndicators()
        {
            if (!_aligning || _angle > 0.01) return;
            var distance = _targetMatrix.Translation - _connectorMatrix.Translation;
            var tgt = _targetVector;// Vector3D.Transform(distance, MatrixD.Transpose(ActiveCockpit.WorldMatrix));
            var x = (float) _xPid.Control(-tgt.X / 10, _pidTimestep);
            var y = (float) _yPid.Control(tgt.Y / 10, _pidTimestep);
            var z = (float) _zPid.Control(-tgt.Z / 10, _pidTimestep);
            _echo += $"{x}\n{y}\n{z}\n";
            SetThrust(Base6Directions.Direction.Left, x);
            SetThrust(Base6Directions.Direction.Up, y);
            SetThrust(Base6Directions.Direction.Forward, z);

        }

        void SetThrust(Base6Directions.Direction direction, float magnitude)
        {
            magnitude = Math.Max(-1, Math.Min(magnitude, 1));
            var reverseIndex = (int)direction;
            var index = reverseIndex + (reverseIndex % 2 == 0 ? 1 : -1);
            if (magnitude < 0)
            {
                magnitude = -magnitude;
                var temp = reverseIndex;
                reverseIndex = index;
                index = temp;
            }
            _thrusterArray[index].ForEach(t => t.ThrustOverridePercentage = magnitude);
            _thrusterArray[reverseIndex].ForEach(t => t.ThrustOverridePercentage = 0);
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
            status += $"Target:\n {_targetName}\n";
            //status += $"{_gyroVector.X:0.00} {_gyroVector.Y:0.00} {_gyroVector.Z:0.00}\n";
            status += $"Translate: {GetTranslation(_targetVector.X,"X")}\n";
            status += $"Translate: {GetTranslation(_targetVector.Y,"Y")}\n";
            status += $"Translate: {GetTranslation(_targetVector.Z,"Z")}\n";
            status += $"Pitch:     {GetAngle(_rotationVector.X, "X")}\n";
            status += $"Yaw:       {GetAngle(_rotationVector.Y, "Y")}\n";
            status += $"Roll:      {GetAngle(_rotationVector.Z, "Z")}\n";
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
