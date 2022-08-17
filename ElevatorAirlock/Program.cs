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

        IMyDoor Gate;
        IMyDoor InnerDoor;
        IMyPistonBase Piston;
        IMyAirVent AirVent;

        FiniteStateMachine StateMachine;

        const float PistonSpeed = 2f;

        const float PistonMaxLimit = 7.2f;
        const float PistonMinLimit = 0.15f;

        const string Toggle = "Toggle"; 

        public Program()
        {
            Gate = GridTerminalSystem.GetBlockWithName("Elevator Gate") as IMyDoor;
            InnerDoor = GridTerminalSystem.GetBlockWithName("Elevator Inner Door") as IMyDoor;
            Piston = GridTerminalSystem.GetBlockWithName("Elevator Piston") as IMyPistonBase;
            AirVent = GridTerminalSystem.GetBlockWithName("Elevator Air Vent") as IMyAirVent;

            

            Piston.MaxLimit = PistonMaxLimit;
            Piston.MinLimit = PistonMinLimit;

            

            var states = new[]
            {
                new State { // 0 open inner door
                    Update = () =>
                    {
                        AirVent.Depressurize = false;
                        InnerDoor.OpenDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, int>
                    {
                        [Toggle] = 1
                    }
                },
                new State { // 1 close inner door
                    EndCondition = () => AirVent.GetOxygenLevel() == 0f,
                    NextState = 2,
                    Update = () =>
                    {
                        AirVent.Depressurize = true;
                        InnerDoor.CloseDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, int>
                    {
                        [Toggle] = 0
                    }
                },
                new State { // 2 open gate
                    EndCondition = () => Gate.Status == DoorStatus.Open,
                    NextState = 3,
                    Update = () =>
                    {
                        Gate.OpenDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, int>
                    {
                        [Toggle] = 5
                    }
                },
                new State { // 3 lower
                    Update = () =>
                    {
                        Piston.Velocity = PistonSpeed;
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, int>
                    {
                        [Toggle] = 4
                    }
                },
                new State { // 4 raise
                    EndCondition = () => Piston.CurrentPosition == PistonMinLimit,
                    NextState = 5,
                    Update = () =>
                    {
                        Piston.Velocity = -PistonSpeed;
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, int>
                    {
                        [Toggle] = 3
                    }
                },
                new State { // 5 close gate
                    EndCondition = () => Gate.Status == DoorStatus.Closed,
                    NextState = 0,
                    Update = () =>
                    {
                        Gate.CloseDoor();
                        return string.Empty;
                    },
                    Triggers = new Dictionary<string, int>
                    {
                        [Toggle] = 2
                    }
                },
            };

            int startIndex;
            startIndex = int.TryParse(Storage, out startIndex) ? startIndex : 0;
            StateMachine = new FiniteStateMachine(startIndex, states);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
            Storage = $"{StateMachine.ActiveStateIndex}";
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0)
            {
                StateMachine.Trigger(argument);
            }
            StateMachine.Update();
            Echo($"{StateMachine.ActiveStateIndex}");
        }
    }
}
