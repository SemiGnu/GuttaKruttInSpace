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
        public class StateMachine<EnumStates, EnumTransitions> 
            where EnumStates : struct
            where EnumTransitions : struct
        {
            public State[] States { get; set; }
            public State ActiveState { get; set; }

            public StateMachine(EnumStates startState, State[] states)
            {
                States = states;
                SetState(startState);
            }

            public string Update()
            {
                if (ActiveState.EndCondition())
                {
                    SetState(ActiveState.NextState);
                }
                return ActiveState.Update();
            }

            public void Trigger(EnumTransitions transitions)
            {
                EnumStates newState;
                if (ActiveState.Triggers.TryGetValue(transitions, out newState))
                {
                    SetState(newState);
                }
            }

            private void SetState(EnumStates state)
            {
                ActiveState = States.First(s => s.Id.Equals(state));
            }
            public class State
            {
                public EnumStates Id { get; set; }
                public Func<bool> EndCondition { get; set; } = () => false;
                public EnumStates NextState { get; set; }
                public Func<string> Update { get; set; } = () => string.Empty;
                public Dictionary<EnumTransitions, EnumStates> Triggers { get; set; } = new Dictionary<EnumTransitions, EnumStates>();
            }
        }

    }
}
