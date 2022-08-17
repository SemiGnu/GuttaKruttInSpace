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
        IMyShipController _cockpit;
        IMyRemoteControl _remote;
        IMyShipConnector _topDock;
        IMyShipConnector _bottomDock;
        IMyShipConnector _topConnector;
        IMyShipConnector _bottomConnector;
        IMyGyro _gyro;
        List<IMyTextSurface> _lcds = new List<IMyTextSurface>();

        MatrixD _topDockMatrix, _bottomDockMatrix;

        List<IMyThrust> _thrusters = new List<IMyThrust>();
        List<IMyThrust>[] _thrusterArray = new List<IMyThrust>[6];

        string _echo;

        bool _translating = false;
        bool _aligning = false;

        double _pTune = 1;
        double _iTune = 0;
        double _dTune = 3.5;

        static double _pidTimestep = 1.0 / 6.0;

        PID _yDownPid = new PID(1, 0, 4.5, _pidTimestep);
        PID _yUpPid = new PID(1.05, 0, 3.85, _pidTimestep);
        PID _xPid = new PID(1, 0, 1, _pidTimestep);
        PID _zPid = new PID(1, 0, 1, _pidTimestep);

        double _dockingOffset = 1.85;
        Vector3D _gyroVector, _targetVector;


        enum States
        {
            Up, Down, Ascending, Descending
        }
        enum Transitions
        {
            Up, Down, Toggle
        }
        StateMachine<States, Transitions> _stateMachine;

        public Program()
        {
            var states = new StateMachine<States, Transitions>.State[] {
                new StateMachine<States, Transitions>.State
                {
                    Id = States.Down,
                    Update = () => "Down",
                    Triggers = new Dictionary<Transitions, States>
                    {
                        [Transitions.Up] = States.Ascending,
                        [Transitions.Toggle] = States.Ascending
                    },
                },
                new StateMachine<States, Transitions>.State
                {
                    Id = States.Ascending,
                    Update = () => {
                        _bottomConnector.Disconnect();
                        SetIndicators(States.Up);
                        CalculateRotationVector();
                        SetGyro();
                        if(_topConnector.Status == MyShipConnectorStatus.Connectable && _targetVector.Length() < 0.05)
                        {
                            TurnOff();
                            _topConnector.Connect();
                        }
                        return $"Ascending";
                    },
                    EndCondition = () => _topConnector.Status == MyShipConnectorStatus.Connected,
                    NextState = States.Up,
                    Triggers = new Dictionary<Transitions, States>
                    {
                        [Transitions.Down] = States.Descending,
                        [Transitions.Toggle] = States.Descending
                    },
                },
                new StateMachine<States, Transitions>.State
                {
                    Id = States.Up,
                    Update = () => "Up",
                    Triggers = new Dictionary<Transitions, States>
                    {
                        [Transitions.Down] = States.Descending,
                        [Transitions.Toggle] = States.Descending
                    },
                },
                new StateMachine<States, Transitions>.State
                {
                    Id = States.Descending,
                    Update = () => {
                        _topConnector.Disconnect();
                        SetIndicators(States.Down);
                        CalculateRotationVector();
                        SetGyro();
                        if(_bottomConnector.Status == MyShipConnectorStatus.Connectable)
                        {
                            TurnOff();
                            _bottomConnector.Connect();
                        }
                        return $"Descending";
                    },
                    EndCondition = () => _bottomConnector.Status == MyShipConnectorStatus.Connected,
                    NextState = States.Down,
                    Triggers = new Dictionary<Transitions, States>
                    {
                        [Transitions.Up] = States.Ascending,
                        [Transitions.Toggle] = States.Ascending
                    },
                },
            };
            _stateMachine = new StateMachine<States, Transitions>(States.Down, states);

            _remote = GridTerminalSystem.GetBlockWithName("Capsule Remote Control") as IMyRemoteControl;
            _cockpit = GridTerminalSystem.GetBlockWithName("Capsule Cockpit") as IMyShipController;
            _topDock = GridTerminalSystem.GetBlockWithName("Capsule Top Dock") as IMyShipConnector;
            _bottomDock = GridTerminalSystem.GetBlockWithName("Capsule Bottom Dock") as IMyShipConnector;
            _topConnector = GridTerminalSystem.GetBlockWithName("Capsule Top Connector") as IMyShipConnector;
            _bottomConnector = GridTerminalSystem.GetBlockWithName("Capsule Bottom Connector") as IMyShipConnector;
            _gyro = GridTerminalSystem.GetBlockWithName("Capsule Gyro") as IMyGyro;

            _thrusterArray = _thrusterArray.Select(ta => new List<IMyThrust>()).ToArray();
            GridTerminalSystem.GetBlocksOfType(_thrusters, t => t.CubeGrid == _remote.CubeGrid);
            var cDir = _remote.Orientation.Forward;
            foreach (var thruster in _thrusters)
            {
                var thrusterDirection = _remote.Orientation.TransformDirectionInverse(thruster.Orientation.Forward);
                _thrusterArray[(int)thrusterDirection].Add(thruster);
            }

            _lcds.Add(Me.GetSurface(0));
            _lcds.Add((_cockpit as IMyTextSurfaceProvider).GetSurface(0));
            _lcds.Add((GridTerminalSystem.GetBlockWithName("Capsule Cock 2") as IMyTextSurfaceProvider).GetSurface(0));


            var newMatrix = _topDock.WorldMatrix;
            newMatrix.Translation += newMatrix.Forward * _dockingOffset;
            _topDockMatrix = MatrixD.CreateFromDir(newMatrix.Backward);
            _topDockMatrix.Translation = newMatrix.Translation;
            newMatrix = _bottomDock.WorldMatrix;
            newMatrix.Translation += newMatrix.Forward * _dockingOffset;
            _bottomDockMatrix = MatrixD.CreateFromDir(newMatrix.Backward);
            _bottomDockMatrix.Translation = newMatrix.Translation;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            Transitions arg;
            if ((updateSource & UpdateType.Trigger) > 0 && Enum.TryParse(argument, out arg))
            {
                _stateMachine.Trigger(arg);
            }
            _echo = _stateMachine.Update();
            Echo(_echo);
            _lcds.ForEach(l => l.WriteText(_echo));
        }

        void TurnOff()
        {
            _thrusters.ForEach(t => t.ThrustOverridePercentage = 0);
            _gyro.GyroOverride = false;
        }

        private void CalculateRotationVector()
        {
            var targetQ = QuaternionD.CreateFromForwardUp(_cockpit.WorldMatrix.Forward, _cockpit.WorldMatrix.Up);
            var capsuleQ = QuaternionD.CreateFromForwardUp(_remote.WorldMatrix.Forward, _remote.WorldMatrix.Up);
            QuaternionD current = QuaternionD.CreateFromRotationMatrix(_remote.WorldMatrix.GetOrientation());
            QuaternionD rotation = targetQ / capsuleQ;
            rotation.Normalize();
            Vector3D axis;
            double angle;

            rotation.GetAxisAngle(out axis, out angle);

            MatrixD worldToCockpit = MatrixD.Invert(_remote.WorldMatrix.GetOrientation());
            MatrixD worldToGyro = MatrixD.Invert(_gyro.WorldMatrix.GetOrientation());
            Vector3D localGyroAxis = Vector3D.Transform(axis, worldToGyro);

            double value = Math.Log(angle + 1, 2);
            localGyroAxis *= value < 0.001 ? 0 : value;
            _gyroVector = localGyroAxis;
        }

        void SetGyro()
        {
            _gyro.GyroOverride = true;
            _gyro.Pitch = (float)-_gyroVector.X;
            _gyro.Yaw = (float)-_gyroVector.Y;
            _gyro.Roll = (float)-_gyroVector.Z;
        }

        void SetIndicators(States state)
        {
            var distance = state == States.Up 
                ? _topConnector.WorldMatrix.Translation - _topDockMatrix.Translation
                : _bottomConnector.WorldMatrix.Translation - _bottomDockMatrix.Translation;
            _targetVector = Vector3D.TransformNormal(distance, MatrixD.Transpose(_remote.WorldMatrix));

            var y = state == States.Up 
                ? (float)_yUpPid.Control(-_targetVector.Y / 10, _pidTimestep)
                : (float)_yDownPid.Control(-_targetVector.Y / 10, _pidTimestep);
            var x = (float)_xPid.Control(_targetVector.X / 10, _pidTimestep);
            var z = (float)_zPid.Control(_targetVector.Z / 10, _pidTimestep);
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
    }
}
