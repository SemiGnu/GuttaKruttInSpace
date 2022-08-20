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
        IMyShipConnector _topConnector, _bottomConnector;
        IMyDoor _gate;
        IMyDoor _stationDoor, _door;
        IMyGyro _gyro;
        List<IMyAirVent> _airVents = new List<IMyAirVent>();
        List<IMyTextSurface> _lcds = new List<IMyTextSurface>();

        MatrixD _topDockMatrix, _bottomDockMatrix;
        QuaternionD _targetQuaternion;

        List<IMyThrust> _thrusters = new List<IMyThrust>();
        List<IMyThrust>[] _thrusterArray = new List<IMyThrust>[6];

        string _echo;

        static double _pidTimestep = 1.0 / 6.0;

        PID _yDownPid = new PID(1, 0, 4.5, _pidTimestep);
        PID _yUpPid = new PID(1.05, 0, 3.85, _pidTimestep);
        PID _xPid = new PID(1, 0, 1, _pidTimestep);
        PID _zPid = new PID(1, 0, 1, _pidTimestep);

        double _dockingOffset = 1.85;
        Vector3D _gyroVector, _distanceVector, _targetVector;

        IMyUnicastListener _listener;
        string _capsuleInfoBroadcastTag = "CAPSULE_INFO_LISTENER";
        string _capsuleDistanceBroadcastTag = "CAPSULE_DISTANCE_LISTENER";
        string _unicastInfoTag = "CAPSULE_INFO_LISTENER";
        string _unicastTriggerTag = "CAPSULE_TRIGGER_LISTENER";
        long _capsuleHubId;


        enum State
        {
            Init, Up, Down, Ascending, Descending, OpeningGate, ClosingGate, OpeningDoors, ClosingDoors
        }
        enum Transitions
        {
            Up, Down, Toggle
        }
        StateMachine<State> _stateMachine;

        

        public Program()
        {
            var states = GetStates();
            _stateMachine = new StateMachine<State>(State.Init, states);

            _listener = IGC.UnicastListener;

            #region Set blocks
            _remote = GridTerminalSystem.GetBlockWithName("Capsule Remote Control") as IMyRemoteControl;
            _topConnector = GridTerminalSystem.GetBlockWithName("Capsule Top Connector") as IMyShipConnector;
            _bottomConnector = GridTerminalSystem.GetBlockWithName("Capsule Bottom Connector") as IMyShipConnector;
            _gyro = GridTerminalSystem.GetBlockWithName("Capsule Gyro") as IMyGyro;
            _door = GridTerminalSystem.GetBlockWithName("Capsule Door") as IMyDoor;

            _thrusterArray = _thrusterArray.Select(ta => new List<IMyThrust>()).ToArray();
            GridTerminalSystem.GetBlocksOfType(_thrusters, t => t.CubeGrid == _remote.CubeGrid);
            var cDir = _remote.Orientation.Forward;
            foreach (var thruster in _thrusters)
            {
                var thrusterDirection = _remote.Orientation.TransformDirectionInverse(thruster.Orientation.Forward);
                _thrusterArray[(int)thrusterDirection].Add(thruster);
            }

            _lcds.Add(Me.GetSurface(0));
            #endregion

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }


        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0)
            {
                _stateMachine.Trigger(argument);
            }
            HandleMessages();
            _echo = _stateMachine.Update();
            //Echo($"{_distanceVector.Y:0.000}");
            _lcds.ForEach(l => l.WriteText(_echo));
        }

        private void HandleMessages()
        {
            while (_listener.HasPendingMessage)
            {
                MyIGCMessage message = _listener.AcceptMessage();
                if (message.Tag == _unicastInfoTag && message.Data is MyTuple<MatrixD, MatrixD, QuaternionD>)
                {
                    var data = (MyTuple<MatrixD, MatrixD, QuaternionD>)message.Data;
                    _capsuleHubId = message.Source;
                    _topDockMatrix = data.Item1;
                    _bottomDockMatrix = data.Item2;
                    _targetQuaternion = data.Item3;
                }
                if(message.Tag == _unicastTriggerTag && message.Data is string)
                {
                    _stateMachine.Trigger((string)message.Data);
                }
            }
        }

        void TurnOff()
        {
            _thrusters.ForEach(t => t.ThrustOverridePercentage = 0);
            _gyro.GyroOverride = false;
        }

        private void CalculateRotationVector()
        {
            var capsuleQuaternion = QuaternionD.CreateFromForwardUp(_remote.WorldMatrix.Forward, _remote.WorldMatrix.Up);
            QuaternionD current = QuaternionD.CreateFromRotationMatrix(_remote.WorldMatrix.GetOrientation());
            QuaternionD rotation = _targetQuaternion / capsuleQuaternion;
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

        void SetIndicators(State state)
        {
            _distanceVector = state == State.Up 
                ? _topConnector.WorldMatrix.Translation - _topDockMatrix.Translation
                : _bottomConnector.WorldMatrix.Translation - _bottomDockMatrix.Translation;
            _targetVector = Vector3D.TransformNormal(_distanceVector, MatrixD.Transpose(_remote.WorldMatrix));

            var y = state == State.Up 
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
        private string Ascend()
        {
            _bottomConnector.Disconnect();
            SetIndicators(State.Up);
            CalculateRotationVector();
            SetGyro();
            if (_topConnector.Status == MyShipConnectorStatus.Connectable && _targetVector.Length() < 0.05)
            {
                TurnOff();
                _topConnector.Connect();
                _stationDoor = GridTerminalSystem.GetBlockWithName("Capsule Top Door") as IMyDoor;
                _gate = GridTerminalSystem.GetBlockWithName("Capsule Top Gate") as IMyDoor;
            }
            var status = $"Ascending\n{_distanceVector.Length():0000}m";
            IGC.SendUnicastMessage(_capsuleHubId, _capsuleDistanceBroadcastTag, status);
            return status;
        }

        private string Descend()
        {
            _topConnector.Disconnect();
            SetIndicators(State.Down);
            CalculateRotationVector();
            SetGyro();
            if (_bottomConnector.Status == MyShipConnectorStatus.Connectable && _targetVector.Length() < 0.05)
            {
                TurnOff();
                _bottomConnector.Connect();
                _stationDoor = GridTerminalSystem.GetBlockWithName("Capsule Bottom Door") as IMyDoor;
                _gate = GridTerminalSystem.GetBlockWithName("Capsule Bottom Gate") as IMyDoor;
            }
            var status = $"Descending\n{_distanceVector.Length():0000}m";
            IGC.SendUnicastMessage(_capsuleHubId, _capsuleDistanceBroadcastTag, status);
            return status;
        }

        private void SetBlocks(State state)
        {

        }

        private StateMachineState<State>[] GetStates()
        {
            State? _null = null;
            return new StateMachineState<State>[] {
                new StateMachineState<State>
                {
                    Id = State.Init,
                    Update = () => {
                        IGC.SendBroadcastMessage(_capsuleInfoBroadcastTag,"");
                        return "Initializing";
                    },
                    NextState = () => _topDockMatrix != default(MatrixD) ? State.Down : _null,
                },
                new StateMachineState<State>
                {
                    Id = State.Down,
                    Update = () => "Docked at\nBase",
                    Triggers = new Dictionary<string, State>
                    {
                        ["Up"] = State.Ascending,
                        ["Toggle"] = State.Ascending
                    },
                },
                new StateMachineState<State>
                {
                    Id = State.Ascending,
                    Update = Ascend,
                    NextState = () => _topConnector.Status == MyShipConnectorStatus.Connected ? State.Up : _null,
                    Triggers = new Dictionary<string, State>
                    {
                        ["Down"] = State.Descending,
                        ["Toggle"] = State.Descending
                    },
                },
                new StateMachineState<State>
                {
                    Id = State.Up,
                    Update = () => "Docked in\nOrbit",
                    Triggers = new Dictionary<string, State>
                    {
                        ["Down"] = State.Descending,
                        ["Toggle"] = State.Descending
                    },
                },
                new StateMachineState<State>
                {
                    Id = State.Descending,
                    Update = Descend,
                    NextState = () => _bottomConnector.Status == MyShipConnectorStatus.Connected ? State.Down : _null,
                    Triggers = new Dictionary<string, State>
                    {
                        ["Up"] = State.Ascending,
                        ["Toggle"] = State.Ascending
                    },
                },
                new StateMachineState<State>
                {
                    Id = State.ClosingGate,
                    Update = () => {
                        _gate.CloseDoor();
                        return "Closing\nGate";
                    },
                    NextState = () => _gate.Status == DoorStatus.Closed ? State.OpeningDoors : _null,
                    Triggers = new Dictionary<string, State>
                    {
                        ["Toggle"] = State.OpeningGate
                    },
                },
                new StateMachineState<State>
                {
                    Id = State.OpeningDoors,
                    Update = () => {
                        _stationDoor.OpenDoor();
                        _door.OpenDoor();
                        _airVents.ForEach(a => a.Depressurize = false);
                        return "Opening\nDoors";
                    },
                    NextState = () => _gate.Status != DoorStatus.Closed ? _null : _topConnector.Status == MyShipConnectorStatus.Connected ? State.Up: State.Down,
                    Triggers = new Dictionary<string, State>
                    {
                        ["Toggle"] = State.ClosingDoors
                    },
                },
            };
        }
    }
}
