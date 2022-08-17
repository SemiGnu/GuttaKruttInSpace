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
        public class FiniteStateMachine
        {
            public State[] States { get; set; }
            public State ActiveState { get; set; }
            public int ActiveStateIndex { get; set; }
            public FiniteStateMachine(int startState, State[] states)
            {
                States = states;
                ActiveState = States[startState];
            }

            public string Update()
            {
                if (ActiveState.EndCondition())
                {
                    SetState(ActiveState.NextState);
                    //return "Updating";
                }
                return ActiveState.Update();
            }

            public void Trigger(string trigger)
            {
                int newState;
                if (ActiveState.Triggers.TryGetValue(trigger, out newState))
                {
                    SetState(newState);
                }
            }

            private void SetState(int stateIndex)
            {
                ActiveStateIndex = stateIndex;
                ActiveState = States[stateIndex];
            } 

        }

        public class State
        {
            public Func<bool> EndCondition { get; set; } = () => false;
            public int NextState { get; set; }
            public Func<string> Update { get; set; } = () => string.Empty;
            public Dictionary<string, int> Triggers { get; set; } = new Dictionary<string, int>();
        }
    }
}
