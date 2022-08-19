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
        public class StateMachine<EnumStates> 
            where EnumStates : struct
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
                var nextState = ActiveState.NextState();
                if (nextState.HasValue)
                {
                    SetState(nextState.Value);
                }
                return ActiveState.Update();
            }

            public void Trigger(string transition)
            {
                EnumStates newState;
                if (ActiveState.Triggers.TryGetValue(transition, out newState))
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
                public Func<string> Update { get; set; } = () => string.Empty;
                public Func<EnumStates?> NextState { get; set; } = () => null;
                public Dictionary<string, EnumStates> Triggers { get; set; } = new Dictionary<string, EnumStates>();
            }
        }

    }
}
