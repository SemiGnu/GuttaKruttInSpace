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
    partial class Program
    {
        public class StateMachine<TState> 
            where TState : struct
        {
            public StateMachineState<TState>[] States { get; set; }
            public StateMachineState<TState> ActiveState { get; set; }

            public StateMachine(TState startState, StateMachineState<TState>[] states)
            {
                States = states;
                SetState(startState);
            }

            public string Update()
            {
                var nextState = ActiveState.NextState();
                if (nextState.HasValue)
                {
                    SetState(nextState.Value);
                }
                return ActiveState.Update();
            }

            public void Trigger(string transition)
            {
                TState newState;
                if (ActiveState.Triggers.TryGetValue(transition, out newState))
                {
                    SetState(newState);
                }
            }

            private void SetState(TState state)
            {
                ActiveState = States.First(s => s.Id.Equals(state));
            }

        }
        public class StateMachineState<EnumState> where EnumState : struct
        {
            public EnumState Id { get; set; }
            public Func<string> Update { get; set; } = () => string.Empty;
            public Func<EnumState?> NextState { get; set; } = () => null;
            public Dictionary<string, EnumState> Triggers { get; set; } = new Dictionary<string, EnumState>();
        }

    }
}
