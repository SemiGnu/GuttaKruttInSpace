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

        IMyDoor _gate;
        IMyDoor _innerDoor;
        IMyPistonBase _piston;
        IMyAirVent _airVent;

        enum State
        {
            Lowering, Raising, OpeningGate, ClosingGate, OpeningDoor, ClosingDoor
        }
        State? _null = null;

        StateMachine<State> _stateMachine;

        const float _pistonSpeed = 2f;

        const float _pistonMaxLimit = 7.2f;
        const float _pistonMinLimit = 0.15f;

        const string _toggle = "Toggle"; 

        public Program()
        {
            _gate = GridTerminalSystem.GetBlockWithName("Elevator Gate") as IMyDoor;
            _innerDoor = GridTerminalSystem.GetBlockWithName("Elevator Inner Door") as IMyDoor;
            _piston = GridTerminalSystem.GetBlockWithName("Elevator Piston") as IMyPistonBase;
            _airVent = GridTerminalSystem.GetBlockWithName("Elevator Air Vent") as IMyAirVent;

            

            _piston.MaxLimit = _pistonMaxLimit;
            _piston.MinLimit = _pistonMinLimit;

            

            var states = new[]
            {
                new StateMachineState<State>{
                    Id = State.OpeningDoor,
                    Update = () =>
                    {
                        _airVent.Depressurize = false;
                        _innerDoor.OpenDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, State>
                    {
                        [_toggle] = State.ClosingDoor
                    }
                },
                new StateMachineState<State>{
                    Id = State.ClosingDoor,
                    NextState = () => _airVent.GetOxygenLevel() == 0f ? State.OpeningGate : _null,
                    Update = () =>
                    {
                        _airVent.Depressurize = true;
                        _innerDoor.CloseDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, State>
                    {
                        [_toggle] = State.OpeningDoor
                    }
                },
                new StateMachineState<State>{
                    Id = State.OpeningGate,
                    NextState = () => _gate.Status == DoorStatus.Open ? State.Lowering : _null,
                    Update = () =>
                    {
                        _gate.OpenDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, State>
                    {
                        [_toggle] = State.ClosingGate
                    }
                },
                new StateMachineState<State>{
                    Id = State.Lowering,
                    Update = () =>
                    {
                        _piston.Velocity = _pistonSpeed;
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, State>
                    {
                        [_toggle] = State.Raising
                    }
                },
                new StateMachineState<State>{
                    Id = State.Raising,
                    NextState = () => _piston.CurrentPosition == _pistonMinLimit ? State.ClosingGate : _null,
                    Update = () =>
                    {
                        _piston.Velocity = -_pistonSpeed;
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, State>
                    {
                        [_toggle] = State.Lowering
                    }
                },
                new StateMachineState<State>{
                    Id = State.ClosingGate,
                    NextState = () => _gate.Status == DoorStatus.Closed ? State.OpeningDoor : _null,
                    Update = () =>
                    {
                        _gate.CloseDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, State>
                    {
                        [_toggle] = State.OpeningGate
                    }
                },
            };

            State startIndex;
            startIndex = Enum.TryParse(Storage, out startIndex) ? startIndex : 0;
            _stateMachine = new StateMachine<State>(startIndex, states);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
            Storage = $"{_stateMachine.ActiveState.Id}";
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0)
            {
                _stateMachine.Trigger(argument);
            }
            _stateMachine.Update();
            Echo($"{_stateMachine.ActiveState.Id}");
        }
    }
}
